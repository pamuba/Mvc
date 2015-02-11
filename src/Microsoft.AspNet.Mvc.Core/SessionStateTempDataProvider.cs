// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Interfaces;
using Microsoft.Framework.Internal;
using Newtonsoft.Json;

namespace Microsoft.AspNet.Mvc
{
    /// <summary>
    /// Provides session-state data to the current <see cref="ITempDataDictionary"/> object.
    /// </summary>
    public class SessionStateTempDataProvider : ITempDataProvider
    {
        private static string TempDataSessionStateKey = "__ControllerTempData";

        /// <inheritdoc />
        public virtual IDictionary<string, object> LoadTempData([NotNull] HttpContext context)
        {
            if (!IsSessionEnabled(context))
            {
                // Session middleware is not enabled. No-op
                return null;
            }

            var session = context.Session;
            byte[] value = null;

            if (session != null && session.TryGetValue(TempDataSessionStateKey, out value))
            {
                var tempDataDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    Encoding.UTF8.GetString(value));

                // If we got it from Session, remove it so that no other request gets it
                session.Remove(TempDataSessionStateKey);
                return tempDataDictionary;
            }

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public virtual void SaveTempData([NotNull] HttpContext context, IDictionary<string, object> values)
        {
            var isDirty = (values != null && values.Count > 0);
            if (isDirty)
            {
                // This will throw if the session middleware is not enabled.
                var session = context.Session;
                session[TempDataSessionStateKey] = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(values));
            }
            else if (IsSessionEnabled(context))
            {
                // This shouldn't throw because session is enabled.
                var session = context.Session;
                session.Remove(TempDataSessionStateKey);
            }
        }

        private static bool IsSessionEnabled(HttpContext context)
       {
            return context.GetFeature<ISessionFeature>() != null;
        }
    }
}