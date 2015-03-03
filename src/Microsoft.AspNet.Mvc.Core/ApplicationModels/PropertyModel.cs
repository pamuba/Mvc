// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNet.Mvc.ModelBinding;
using Microsoft.Framework.Internal;

namespace Microsoft.AspNet.Mvc.ApplicationModels
{
    [DebuggerDisplay("PropertyModel: Name={PropertyName}")]
    public class PropertyModel
    {
        public PropertyModel([NotNull] PropertyInfo propertyInfo,
                              [NotNull] IReadOnlyList<object> attributes)
        {
            PropertyInfo = propertyInfo;

            Attributes = new List<object>(attributes);
        }

        public PropertyModel([NotNull] PropertyModel other)
        {
            Controller = other.Controller;
            Attributes = new List<object>(other.Attributes);
            BinderMetadata = other.BinderMetadata;
            PropertyInfo = other.PropertyInfo;
            PropertyName = other.PropertyName;
        }

        public ControllerModel Controller { get; set; }

        public IReadOnlyList<object> Attributes { get; }

        public IBinderMetadata BinderMetadata { get; set; }

        public PropertyInfo PropertyInfo { get; private set; }

        public string PropertyName { get; set; }
    }
}