// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.OData.Common;
using Microsoft.OpenApi.OData.Edm;
using Microsoft.OpenApi.OData.Generator;
using Microsoft.OpenApi.OData.Vocabulary.Capabilities;

namespace Microsoft.OpenApi.OData.Operation;

/// <summary>
/// Retrieves a .../namespace.typename get
/// </summary>
internal class ODataTypeCastGetOperationHandler : OperationHandler
{
	/// <inheritdoc/>
	public override OperationType OperationType => OperationType.Get;

	/// <summary>
	/// Gets/sets the segment before cast.
	/// this segment could be "entity set", "Collection property", etc.
	/// </summary>
	internal ODataSegment LastSecondSegment { get; set; }

	private bool isKeySegment;
	private bool IsSingleElement 
	{
		get => isKeySegment ||  //TODO add is singleton when implemented
					(navigationProperty != null &&
					!navigationProperty.Type.IsCollection() &&
					entitySet == null);
	}
	private NavigationPropertyRestriction restriction;
	private IEdmEntitySet entitySet;
	private IEdmNavigationProperty navigationProperty;
	private IEdmEntityType parentEntityType;
	private IEdmEntityType targetEntityType;
	private const int SecondLastSegmentIndex = 2;
	/// <inheritdoc/>
	protected override void Initialize(ODataContext context, ODataPath path)
	{
		base.Initialize(context, path);

		// get the last second segment
		int count = path.Segments.Count;
		if(count >= SecondLastSegmentIndex)
			LastSecondSegment = path.Segments.ElementAt(count - SecondLastSegmentIndex);

		parentEntityType = LastSecondSegment.EntityType;
		if(LastSecondSegment is ODataNavigationPropertySegment navigationPropertySegment)
		{
			SetNavigationPropertyAndRestrictionFromNavigationSegment(navigationPropertySegment, path);
		}
		else if(LastSecondSegment is ODataNavigationSourceSegment sourceSegment && sourceSegment.NavigationSource is IEdmEntitySet)
		{
			SetEntitySetAndRestrictionFromSourceSegment(sourceSegment);
		}
		else if(LastSecondSegment is ODataKeySegment)
		{
			isKeySegment = true;
			var thirdLastSegment = path.Segments.ElementAt(count - SecondLastSegmentIndex - 1);
			if(thirdLastSegment is ODataNavigationPropertySegment navigationPropertySegment1)
			{
				SetNavigationPropertyAndRestrictionFromNavigationSegment(navigationPropertySegment1, path);
			}
			else if(thirdLastSegment is ODataNavigationSourceSegment sourceSegment1)
			{
				SetEntitySetAndRestrictionFromSourceSegment(sourceSegment1);
			}
		}
		//TODO singleton
		if(path.Last() is ODataTypeCastSegment oDataTypeCastSegment)
		{
			targetEntityType = oDataTypeCastSegment.EntityType;
		}
		else throw new NotImplementedException($"type cast type {path.Last().GetType().FullName} not implemented");
	}

	private void SetNavigationPropertyAndRestrictionFromNavigationSegment(ODataNavigationPropertySegment navigationPropertySegment, ODataPath path)
	{
		navigationProperty = navigationPropertySegment.NavigationProperty;
		var navigationPropertyPath = string.Join("/",
			Path.Segments.Where(s => !(s is ODataKeySegment || s is ODataNavigationSourceSegment
									|| s is ODataStreamContentSegment || s is ODataStreamPropertySegment)).Select(e => e.Identifier));

		if(path.FirstSegment is ODataNavigationSourceSegment navigationSourceSegment)
		{
			NavigationRestrictionsType navigation = navigationSourceSegment.NavigationSource switch {
				IEdmEntitySet entitySet => Context.Model.GetRecord<NavigationRestrictionsType>(entitySet, CapabilitiesConstants.NavigationRestrictions),
				IEdmSingleton singleton => Context.Model.GetRecord<NavigationRestrictionsType>(singleton, CapabilitiesConstants.NavigationRestrictions),
				_ => null
			};

			if (navigation?.RestrictedProperties != null)
			{
				restriction = navigation.RestrictedProperties.FirstOrDefault(r => r.NavigationProperty != null && r.NavigationProperty == navigationPropertyPath);
			}
		}
	}

	private void SetEntitySetAndRestrictionFromSourceSegment(ODataNavigationSourceSegment sourceSegment)
	{
		if(sourceSegment.NavigationSource is IEdmEntitySet eSet)
		{
			entitySet = eSet;
			NavigationRestrictionsType navigation = Context.Model.GetRecord<NavigationRestrictionsType>(eSet, CapabilitiesConstants.NavigationRestrictions);
			if (navigation?.RestrictedProperties != null)
			{
				restriction = navigation.RestrictedProperties.FirstOrDefault(r => r.NavigationProperty == null);
			}
		}
	}

	/// <inheritdoc/>
	protected override void SetBasicInfo(OpenApiOperation operation)
	{
		// Summary
		if(IsSingleElement)
			operation.Summary = $"Get the item of type {parentEntityType.ShortQualifiedName()} as {targetEntityType.ShortQualifiedName()}";
		else
			operation.Summary = $"Get the items of type {targetEntityType.ShortQualifiedName()} in the {parentEntityType.ShortQualifiedName()} collection";

		// OperationId
		if (Context.Settings.EnableOperationId)
		{
			operation.OperationId = $"Get.{parentEntityType.ShortQualifiedName()}.As.{targetEntityType.ShortQualifiedName()}";
		}

		base.SetBasicInfo(operation);
	}

	/// <inheritdoc/>
	protected override void SetResponses(OpenApiOperation operation)
	{
		if(IsSingleElement)
			SetSingleResponse(operation);
		else
			SetCollectionResponse(operation);

		operation.Responses.Add(Constants.StatusCodeDefault, Constants.StatusCodeDefault.GetResponse());

		base.SetResponses(operation);
	}
	private void SetCollectionResponse(OpenApiOperation operation)
	{
		OpenApiSchema schema = null;
		if (Context.Settings.EnableDerivedTypesReferencesForResponses)
		{
			schema = EdmModelHelper.GetDerivedTypesReferenceSchema(parentEntityType, Context.Model);
		}

		if (schema == null)
		{
			schema = new OpenApiSchema
			{
				Reference = new OpenApiReference
				{
					Type = ReferenceType.Schema,
					Id = $"{parentEntityType.FullName()}.To.{targetEntityType.FullName()}"
				}
			};
		}

		var properties = new Dictionary<string, OpenApiSchema>
		{
			{
				"value",
				new OpenApiSchema
				{
					Type = "array",
					Items = schema
				}
			}
		};

		if (Context.Settings.EnablePagination)
		{
			properties.Add(
				"@odata.nextLink",
				new OpenApiSchema
				{
					Type = "string"
				});
		}

		operation.Responses = new OpenApiResponses
		{
			{
				Constants.StatusCode200,
				new OpenApiResponse
				{
					Description = "Retrieved entities",
					Content = new Dictionary<string, OpenApiMediaType>
					{
						{
							Constants.ApplicationJsonMediaType,
							new OpenApiMediaType
							{
								Schema = new OpenApiSchema
								{
									Title = $"Collection of items of type {targetEntityType.ShortQualifiedName()} in the {parentEntityType.ShortQualifiedName()} collection",
									Type = "object",
									Properties = properties
								}
							}
						}
					}
				}
			}
		};
	}
	private void SetSingleResponse(OpenApiOperation operation)
	{
		OpenApiSchema schema = null;

		if (Context.Settings.EnableDerivedTypesReferencesForResponses)
		{
			schema = EdmModelHelper.GetDerivedTypesReferenceSchema(targetEntityType, Context.Model);
		}

		if (schema == null)
		{
			schema = new OpenApiSchema
			{
				Reference = new OpenApiReference
				{
					Type = ReferenceType.Schema,
					Id = targetEntityType.FullName()
				}
			};
		}
		operation.Responses = new OpenApiResponses
		{
			{
				Constants.StatusCode200,
				new OpenApiResponse
				{
					Description = "Result entities",
					Content = new Dictionary<string, OpenApiMediaType>
					{
						{
							Constants.ApplicationJsonMediaType,
							new OpenApiMediaType
							{
								Schema = schema
							}
						}
					},
				}
			}
		};
	}
	/// <inheritdoc/>
	protected override void SetTags(OpenApiOperation operation)
	{
		IList<string> items = new List<string>
		{
			parentEntityType.Name,
			targetEntityType.Name,
		};

		string name = string.Join(".", items);
		OpenApiTag tag = new()
		{
			Name = name
		};
		if(!IsSingleElement)
			tag.Extensions.Add(Constants.xMsTocType, new OpenApiString("page"));
		operation.Tags.Add(tag);

		Context.AppendTag(tag);

		base.SetTags(operation);
	}
	/// <inheritdoc/>
	protected override void SetParameters(OpenApiOperation operation)
	{
		base.SetParameters(operation);

		if(navigationProperty != null) {
			if (IsSingleElement)
			{
				new OpenApiParameter[] {
						Context.CreateSelect(navigationProperty),
						Context.CreateExpand(navigationProperty),
					}
				.Where(x => x != null)
				.ToList()
				.ForEach(p => operation.Parameters.Add(p));
			}
			else
			{
				GetParametersForAnnotableOfMany(navigationProperty)
				.Union(
					new OpenApiParameter[] {
						Context.CreateOrderBy(navigationProperty),
						Context.CreateSelect(navigationProperty),
						Context.CreateExpand(navigationProperty),
					})
				.Where(x => x != null)
				.ToList()
				.ForEach(p => operation.Parameters.Add(p));
			}
		}
		else if(entitySet != null)
		{
			if(IsSingleElement)
			{
				new OpenApiParameter[] {
						Context.CreateSelect(entitySet),
						Context.CreateExpand(entitySet),
					}
				.Where(x => x != null)
				.ToList()
				.ForEach(p => operation.Parameters.Add(p));
			}
			else
			{
				GetParametersForAnnotableOfMany(entitySet)
				.Union(
					new OpenApiParameter[] {
						Context.CreateOrderBy(entitySet),
						Context.CreateSelect(entitySet),
						Context.CreateExpand(entitySet),
					})
				.Where(x => x != null)
				.ToList()
				.ForEach(p => operation.Parameters.Add(p));
			}
		}
	}
	private IEnumerable<OpenApiParameter> GetParametersForAnnotableOfMany(IEdmVocabularyAnnotatable annotable) 
	{
		// Need to verify that TopSupported or others should be applied to navigation source.
		// So, how about for the navigation property.
		return new OpenApiParameter[] {
			Context.CreateTop(annotable),
			Context.CreateSkip(annotable),
			Context.CreateSearch(annotable),
			Context.CreateFilter(annotable),
			Context.CreateCount(annotable),
		};
	}

	protected override void SetSecurity(OpenApiOperation operation)
	{
		if (restriction == null || restriction.ReadRestrictions == null)
		{
			return;
		}

		ReadRestrictionsBase readBase = restriction.ReadRestrictions;

		operation.Security = Context.CreateSecurityRequirements(readBase.Permissions).ToList();
	}

	protected override void SetExtensions(OpenApiOperation operation)
	{
		if (Context.Settings.EnablePagination && !IsSingleElement)
		{
			OpenApiObject extension = new()
			{
				{ "nextLinkName", new OpenApiString("@odata.nextLink")},
				{ "operationName", new OpenApiString(Context.Settings.PageableOperationName)}
			};

			operation.Extensions.Add(Constants.xMsPageable, extension);
		}

		base.SetExtensions(operation);
	}
}