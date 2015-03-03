﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Framework.Internal;

namespace Microsoft.AspNet.Mvc.ModelBinding.Metadata
{
    public class DefaultModelMetadataProvider : IModelMetadataProvider
    {
        private readonly ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataDetailsCache> _typesCache = new ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataDetailsCache>();
        private readonly ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataDetailsCache> _parametersCache = new ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataDetailsCache>();
        private readonly ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataDetailsCache[]> _propertiesCache = new ConcurrentDictionary<ModelMetadataIdentity, ModelMetadataDetailsCache[]>();

        public DefaultModelMetadataProvider(ICompositeModelMetadataDetailsProvider detailsProvider)
        {
            DetailsProvider = detailsProvider;
        }

        protected ICompositeModelMetadataDetailsProvider DetailsProvider { get; }

        public virtual ModelMetadata GetMetadataForParameter(
            [NotNull] ParameterInfo parameterInfo,
            [NotNull] IEnumerable<object> attributes)
        {
            var key = ModelMetadataIdentity.ForParameter(parameterInfo);

            ModelMetadataDetailsCache entry;
            if (!_parametersCache.TryGetValue(key, out entry))
            {
                entry = CreateParameterCacheEntry(key, attributes);
                entry = _parametersCache.GetOrAdd(key, entry);
            }

            return CreateModelMetadata(entry);
        }

        public virtual IEnumerable<ModelMetadata> GetMetadataForProperties([NotNull]Type modelType)
        {
            var key = ModelMetadataIdentity.ForType(modelType);

            var propertyEntries = _propertiesCache.GetOrAdd(key, CreatePropertyCacheEntries);

            var properties = new ModelMetadata[propertyEntries.Length];
            for (var i = 0; i < properties.Length; i++)
            {
                properties[i] = CreateModelMetadata(propertyEntries[i]);
            }

            return properties;
        }

        public virtual ModelMetadata GetMetadataForType([NotNull] Type modelType)
        {
            var key = ModelMetadataIdentity.ForType(modelType);

            var entry = _typesCache.GetOrAdd(key, CreateTypeCacheEntry);
            return CreateModelMetadata(entry);
        }

        protected virtual ModelMetadata CreateModelMetadata(ModelMetadataDetailsCache entry)
        {
            return new DefaultModelMetadata(this, DetailsProvider, entry);
        }

        protected virtual ModelMetadataDetailsCache[] CreatePropertyCacheEntries([NotNull] ModelMetadataIdentity key)
        {
            var properties = PropertyHelper.GetProperties(key.ModelType);

            var propertyEntries = new ModelMetadataDetailsCache[properties.Length];
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var propertyKey = ModelMetadataIdentity.ForProperty(
                    property.Property.PropertyType,
                    property.Name,
                    key.ModelType);

                var attributes = new List<object>(ModelAttributes.GetAttributesForProperty(
                    key.ModelType, 
                    property.Property));

                propertyEntries[i] = new ModelMetadataDetailsCache(propertyKey, attributes);
                if (property.Property.CanRead && property.Property.GetMethod?.IsPrivate == true)
                {
                    propertyEntries[i].PropertyAccessor = PropertyHelper.MakeFastPropertyGetter(property.Property);
                }

                if (property.Property.CanWrite && property.Property.SetMethod?.IsPrivate == true)
                {
                    propertyEntries[i].PropertySetter = PropertyHelper.MakeFastPropertySetter(property.Property);
                }
            }

            return propertyEntries;
        }

        protected virtual ModelMetadataDetailsCache CreateTypeCacheEntry([NotNull] ModelMetadataIdentity key)
        {
            var attributes = new List<object>(ModelAttributes.GetAttributesForType(key.ModelType));
            return new ModelMetadataDetailsCache(key, attributes);
        }

        protected virtual ModelMetadataDetailsCache CreateParameterCacheEntry(
            [NotNull] ModelMetadataIdentity key,
            [NotNull] IEnumerable<object> attributes)
        {
            var allAttributes = new List<object>();

            if (attributes != null)
            {
                allAttributes.AddRange(attributes);
            }

            allAttributes.AddRange(ModelAttributes.GetAttributesForParameter(key.ParameterInfo));

            return new ModelMetadataDetailsCache(key, allAttributes);
        }
    }
}