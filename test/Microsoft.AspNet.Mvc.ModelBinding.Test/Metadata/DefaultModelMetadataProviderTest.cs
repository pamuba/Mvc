﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Testing;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.AspNet.Mvc.ModelBinding.Metadata
{
    public class DefaultModelMetadataProviderTest
    {
        [Fact]
        public void GetMetadataForType_IncludesAttributes()
        {
            // Arrange
            var provider = new EmptyModelMetadataProvider();

            // Act
            var metadata = provider.GetMetadataForType(typeof(ModelType));

            // Assert
            var defaultMetadata = Assert.IsType<DefaultModelMetadata>(metadata);

            var attribute = Assert.IsType<ModelAttribute>(Assert.Single(defaultMetadata.Attributes));
            Assert.Equal("OnType", attribute.Value);
        }

        // The attributes and other 'details' are cached
        [Fact]
        public void GetMetadataForType_Cached()
        {
            // Arrange
            var provider = new EmptyModelMetadataProvider();

            // Act
            var metadata1 = Assert.IsType<DefaultModelMetadata>(provider.GetMetadataForType(typeof(ModelType)));
            var metadata2 = Assert.IsType<DefaultModelMetadata>(provider.GetMetadataForType(typeof(ModelType)));

            // Assert
            Assert.Same(metadata1.Attributes, metadata2.Attributes);
            Assert.Same(metadata1.BindingMetadata, metadata2.BindingMetadata);
            Assert.Same(metadata1.DisplayMetadata, metadata2.DisplayMetadata);
            Assert.Same(metadata1.ValidationMetadata, metadata2.ValidationMetadata);
        }

        [Fact]
        public void GetMetadataForProperties_IncludesAllProperties()
        {
            // Arrange
            var provider = new EmptyModelMetadataProvider();

            // Act
            var metadata = provider.GetMetadataForProperties(typeof(ModelType)).ToArray();

            // Assert
            Assert.Equal(2, metadata.Length);
            Assert.Single(metadata, m => m.PropertyName == "Property1");
            Assert.Single(metadata, m => m.PropertyName == "Property2");
        }

        [Fact]
        public void GetMetadataForProperties_IncludesAllProperties_ExceptIndexer()
        {
            // Arrange
            var provider = new EmptyModelMetadataProvider();

            // Act
            var metadata = provider.GetMetadataForProperties(typeof(ModelTypeWithIndexer)).ToArray();

            // Assert
            Assert.Equal(1, metadata.Length);
            Assert.Single(metadata, m => m.PropertyName == "Property1");
        }

        [Fact]
        public void GetMetadataForProperties_Cached()
        {
            // Arrange
            var provider = new EmptyModelMetadataProvider();

            // Act
            var metadata1 = provider.GetMetadataForProperties(typeof(ModelType)).Cast<DefaultModelMetadata>().ToArray();
            var metadata2 = provider.GetMetadataForProperties(typeof(ModelType)).Cast<DefaultModelMetadata>().ToArray();

            // Assert
            for (var i = 0; i < metadata1.Length; i++)
            {
                Assert.Same(metadata1[i].Attributes, metadata2[i].Attributes);
                Assert.Same(metadata1[i].BindingMetadata, metadata2[i].BindingMetadata);
                Assert.Same(metadata1[i].DisplayMetadata, metadata2[i].DisplayMetadata);
                Assert.Same(metadata1[i].ValidationMetadata, metadata2[i].ValidationMetadata);
            }
        }

        [Fact]
        public void GetMetadataForProperties_IncludesMergedAttributes()
        {
            // Arrange
            var provider = new EmptyModelMetadataProvider();

            // Act
            var metadata = provider.GetMetadataForProperties(typeof(ModelType)).First();

            // Assert
            var defaultMetadata = Assert.IsType<DefaultModelMetadata>(metadata);

            var attributes = defaultMetadata.Attributes.ToArray();
            Assert.Equal("OnProperty", Assert.IsType<ModelAttribute>(attributes[0]).Value);
            Assert.Equal("OnPropertyType", Assert.IsType<ModelAttribute>(attributes[1]).Value);
        }

        [Fact]
        public void GetMetadataForParameter_IncludesMergedAttributes()
        {
            // Arrange
            var provider = new EmptyModelMetadataProvider();

            var methodInfo = GetType().GetMethod(
                "GetMetadataForParameterTestMethod", 
                BindingFlags.Instance | BindingFlags.NonPublic);
            var parameterInfo = methodInfo.GetParameters().Where(p => p.Name == "parameter").Single();

            var additionalAttributes = new object[]
            {
                new ModelAttribute("Extra"),
            };

            // Act
            var metadata = provider.GetMetadataForParameter(parameterInfo, additionalAttributes);

            // Assert
            var defaultMetadata = Assert.IsType<DefaultModelMetadata>(metadata);

            var attributes = defaultMetadata.Attributes.ToArray();
            Assert.Equal("Extra", Assert.IsType<ModelAttribute>(attributes[0]).Value);
            Assert.Equal("OnParameter", Assert.IsType<ModelAttribute>(attributes[1]).Value);
            Assert.Equal("OnType", Assert.IsType<ModelAttribute>(attributes[2]).Value);
        }

        // The 'attributes' are assumed to be the same every time. We can safely omit them after
        // the first call.
        [Fact]
        public void GetMetadataForParameter_Cached()
        {
            // Arrange
            var provider = new EmptyModelMetadataProvider();

            var methodInfo = GetType().GetMethod(
                "GetMetadataForParameterTestMethod",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var additionalAttributes = new object[]
            {
                new ModelAttribute("Extra"),
            };

            // Act
            var metadata1 = Assert.IsType<DefaultModelMetadata>(provider.GetMetadataForParameter(
                methodInfo.GetParameters().Where(p => p.Name == "parameter").Single(),
                additionalAttributes));
            var metadata2 = Assert.IsType<DefaultModelMetadata>(provider.GetMetadataForParameter(
                methodInfo.GetParameters().Where(p => p.Name == "parameter").Single(),
                attributes: null));

            // Assert
            Assert.Same(metadata1.Attributes, metadata2.Attributes);
            Assert.Same(metadata1.BindingMetadata, metadata2.BindingMetadata);
            Assert.Same(metadata1.DisplayMetadata, metadata2.DisplayMetadata);
            Assert.Same(metadata1.ValidationMetadata, metadata2.ValidationMetadata);
        }

        [Model("OnType")]
        private class ModelType
        {
            [Model("OnProperty")]
            public PropertyType Property1 { get; }

            public PropertyType Property2 { get; set; }
        }

        [Model("OnPropertyType")]
        private class PropertyType
        {
        }

        private class ModelAttribute : Attribute
        {
            public ModelAttribute(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private class ModelTypeWithIndexer
        {
            public PropertyType this[string key] { get { return null; } }

            public PropertyType Property1 { get; set; }
        }

        private void GetMetadataForParameterTestMethod([Model("OnParameter")] ModelType parameter)
        {
        }
    }
}