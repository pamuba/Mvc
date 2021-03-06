﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Mvc.TagHelpers.Internal;
using Microsoft.AspNet.Razor.Runtime.TagHelpers;
using Microsoft.Framework.Cache.Memory;
using Microsoft.Framework.Logging;
using Microsoft.Framework.WebEncoders;

namespace Microsoft.AspNet.Mvc.TagHelpers
{
    /// <summary>
    /// <see cref="ITagHelper"/> implementation targeting &lt;script&gt; elements that supports fallback src paths.
    /// </summary>
    /// <remarks>
    /// <see cref="FallbackSrc" /> and <see cref="FallbackTestExpression" /> are required to not be
    /// <c>null</c> or empty to process.
    /// </remarks>
    public class ScriptTagHelper : TagHelper
    {
        private const string SrcIncludeAttributeName = "asp-src-include";
        private const string SrcExcludeAttributeName = "asp-src-exclude";
        private const string FallbackSrcAttributeName = "asp-fallback-src";
        private const string FallbackSrcIncludeAttributeName = "asp-fallback-src-include";
        private const string FallbackSrcExcludeAttributeName = "asp-fallback-src-exclude";
        private const string FallbackTestExpressionAttributeName = "asp-fallback-test";
        private const string SrcAttributeName = "src";

        private static readonly ModeAttributes<Mode>[] ModeDetails = new[] {
            // Globbed src (include only)
            ModeAttributes.Create(Mode.GlobbedSrc, new [] { SrcIncludeAttributeName }),
            // Globbed src (include & exclude)
            ModeAttributes.Create(Mode.GlobbedSrc, new [] { SrcIncludeAttributeName, SrcExcludeAttributeName }),
            // Fallback with static src
            ModeAttributes.Create(
                Mode.Fallback, new[]
                {
                    FallbackSrcAttributeName,
                    FallbackTestExpressionAttributeName
                }),
            // Fallback with globbed src (include only)
            ModeAttributes.Create(
                Mode.Fallback, new[] {
                    FallbackSrcIncludeAttributeName,
                    FallbackTestExpressionAttributeName
                }),
            // Fallback with globbed src (include & exclude)
            ModeAttributes.Create(
                Mode.Fallback, new[] {
                    FallbackSrcIncludeAttributeName,
                    FallbackSrcExcludeAttributeName,
                    FallbackTestExpressionAttributeName
                }),
        };

        private enum Mode
        {
            /// <summary>
            /// Just performing file globbing search for the src, rendering a separate &lt;script&gt; for each match.
            /// </summary>
            GlobbedSrc = 0,
            /// <summary>
            /// Rendering a fallback block if primary javascript fails to load. Will also do globbing for both the
            /// primary and fallback srcs if the appropriate properties are set.
            /// </summary>
            Fallback = 1
        }

        /// <summary>
        /// A comma separated list of globbed file patterns of JavaScript scripts to load.
        /// The glob patterns are assessed relative to the application's 'webroot' setting.
        /// </summary>
        [HtmlAttributeName(SrcIncludeAttributeName)]
        public string SrcInclude { get; set; }

        /// <summary>
        /// A comma separated list of globbed file patterns of JavaScript scripts to exclude from loading.
        /// The glob patterns are assessed relative to the application's 'webroot' setting.
        /// Must be used in conjunction with <see cref="SrcInclude"/>.
        /// </summary>
        [HtmlAttributeName(SrcExcludeAttributeName)]
        public string SrcExclude { get; set; }

        /// <summary>
        /// The URL of a Script tag to fallback to in the case the primary one fails.
        /// </summary>
        [HtmlAttributeName(FallbackSrcAttributeName)]
        public string FallbackSrc { get; set; }

        /// <summary>
        /// A comma separated list of globbed file patterns of JavaScript scripts to fallback to in the case the
        /// primary one fails.
        /// The glob patterns are assessed relative to the application's 'webroot' setting.
        /// </summary>
        [HtmlAttributeName(FallbackSrcIncludeAttributeName)]
        public string FallbackSrcInclude { get; set; }

        /// <summary>
        /// A comma separated list of globbed file patterns of JavaScript scripts to exclude from the fallback list, in
        /// the case the primary one fails.
        /// The glob patterns are assessed relative to the application's 'webroot' setting.
        /// Must be used in conjunction with <see cref="FallbackSrcInclude"/>.
        /// </summary>
        [HtmlAttributeName(FallbackSrcExcludeAttributeName)]
        public string FallbackSrcExclude { get; set; }

        /// <summary>
        /// The script method defined in the primary script to use for the fallback test.
        /// </summary>
        [HtmlAttributeName(FallbackTestExpressionAttributeName)]
        public string FallbackTestExpression { get; set; }

        // Protected to ensure subclasses are correctly activated. Internal for ease of use when testing.
        [Activate]
        protected internal ILogger<ScriptTagHelper> Logger { get; set; }

        [Activate]
        protected internal IHostingEnvironment HostingEnvironment { get; set; }

        [Activate]
        protected internal ViewContext ViewContext { get; set; }

        [Activate]
        protected internal IMemoryCache Cache { get; set; }

        // Internal for ease of use when testing.
        protected internal GlobbingUrlBuilder GlobbingUrlBuilder { get; set; }

        [Activate]
        protected internal IHtmlEncoder HtmlEncoder { get; set; }

        /// <inheritdoc />
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var modeResult = AttributeMatcher.DetermineMode(context, ModeDetails);

            modeResult.LogDetails(Logger, this, context.UniqueId, ViewContext.View.Path);

            if (!modeResult.FullMatches.Any())
            {
                // No attributes matched so we have nothing to do
                return;
            }

            // Get the highest matched mode
            var mode = modeResult.FullMatches.Select(match => match.Mode).Max();

            // NOTE: Values in TagHelperOutput.Attributes are already HtmlEncoded
            var attributes = new Dictionary<string, string>(output.Attributes);
            
            var builder = new StringBuilder();
            var originalContent = await context.GetChildContentAsync();

            if (mode == Mode.Fallback && string.IsNullOrEmpty(SrcInclude))
            {
                // No globbing to do, just build a <script /> tag to match the original one in the source file
                BuildScriptTag(originalContent, attributes, builder);
            }
            else
            {
                BuildGlobbedScriptTags(originalContent, attributes, builder);
            }

            if (mode == Mode.Fallback)
            {
                BuildFallbackBlock(attributes, builder);
            }

            // We've taken over tag rendering, so prevent rendering the outer tag
            output.TagName = null;
            output.Content = builder.ToString();
        }

        private void BuildGlobbedScriptTags(
            string originalContent,
            IDictionary<string, string> attributes,
            StringBuilder builder)
        {
            // Build a <script> tag for each matched src as well as the original one in the source file
            string staticSrc;
            attributes.TryGetValue("src", out staticSrc);

            EnsureGlobbingUrlBuilder();
            var urls = GlobbingUrlBuilder.BuildUrlList(staticSrc, SrcInclude, SrcExclude);

            foreach (var url in urls)
            {
                attributes["src"] = HtmlEncoder.HtmlEncode(url);
                var content = string.Equals(url, staticSrc, StringComparison.OrdinalIgnoreCase)
                    ? originalContent
                    : string.Empty;
                BuildScriptTag(content, attributes, builder);
            }
        }

        private void BuildFallbackBlock(IDictionary<string, string> attributes, StringBuilder builder)
        {
            EnsureGlobbingUrlBuilder();

            var fallbackSrcs = GlobbingUrlBuilder.BuildUrlList(FallbackSrc, FallbackSrcInclude, FallbackSrcExclude);

            if (fallbackSrcs.Any())
            {
                // Build the <script> tag that checks the test method and if it fails, renders the extra script.
                builder.AppendLine()
                       .Append("<script>(")
                       .Append(FallbackTestExpression)
                       .Append("||document.write(\"");

                foreach (var src in fallbackSrcs)
                {
                    builder.Append("<script");

                    if (!attributes.ContainsKey("src"))
                    {
                        AppendSrc(builder, "src", src);
                    }

                    foreach (var attribute in attributes)
                    {
                        if (!attribute.Key.Equals(SrcAttributeName, StringComparison.OrdinalIgnoreCase))
                        {
                            var encodedKey = JavaScriptStringEncoder.Default.JavaScriptStringEncode(attribute.Key);
                            var encodedValue = JavaScriptStringEncoder.Default.JavaScriptStringEncode(attribute.Value);

                            builder.AppendFormat(
                                CultureInfo.InvariantCulture,
                                " {0}=\\\"{1}\\\"",
                                encodedKey,
                                encodedValue);
                        }
                        else
                        {
                            AppendSrc(builder, attribute.Key, src);
                        }
                    }

                    builder.Append("><\\/script>");
                }
                
                builder.Append("\"));</script>");
            }
        }

        private void EnsureGlobbingUrlBuilder()
        {
            if (GlobbingUrlBuilder == null)
            {
                GlobbingUrlBuilder = new GlobbingUrlBuilder(
                    HostingEnvironment.WebRootFileProvider,
                    Cache,
                    ViewContext.HttpContext.Request.PathBase);
            }
        }

        private static void BuildScriptTag(
            string content,
            IDictionary<string, string> attributes,
            StringBuilder builder)
        {
            builder.Append("<script");

            foreach (var attribute in attributes)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, " {0}=\"{1}\"", attribute.Key, attribute.Value);
            }

            builder.Append(">")
                   .Append(content)
                   .Append("</script>");
        }

        private void AppendSrc(StringBuilder content, string srcKey, string srcValue)
        {
            // Append src attribute in the original place and replace the content the fallback content
            // No need to encode the key because we know it is "src".
            content.Append(" ")
                   .Append(srcKey)
                   .Append("=\\\"")
                   .Append(HtmlEncoder.HtmlEncode(srcValue))
                   .Append("\\\"");
        }
    }
}
