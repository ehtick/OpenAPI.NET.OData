﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;

namespace Microsoft.OpenApi.OData.Edm
{
    /// <summary>
    /// Stream segment.
    /// </summary>
    public class ODataStreamContentSegment : ODataSegment
    {
        /// <inheritdoc />
        public override IEdmEntityType EntityType => null;
        /// <inheritdoc />
        public override ODataSegmentKind Kind => ODataSegmentKind.StreamContent;

        /// <inheritdoc />
        public override string Identifier => "$value";

        /// <inheritdoc />
		public override IEnumerable<IEdmVocabularyAnnotatable> GetAnnotables()
		{
			return Enumerable.Empty<IEdmVocabularyAnnotatable>();
		}

		/// <inheritdoc />
		public override string GetPathItemName(OpenApiConvertSettings settings, HashSet<string> parameters) => "$value";
    }
}