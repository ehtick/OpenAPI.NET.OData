﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using Microsoft.OpenApi.OData.Tests;
using Xunit;

namespace Microsoft.OpenApi.OData.Edm.Tests
{
    public class ODataPathProviderTests
    {
        [Fact]
        public void GetPathsForEmptyEdmModelReturnsEmptyPaths()
        {
            // Arrange
            IEdmModel model = new EdmModel();
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Empty(paths);
        }

        [Fact]
        public void GetPathsForGraphBetaModelReturnsAllPaths()
        {
            // Arrange
            IEdmModel model = EdmModelHelper.GraphBetaModel;
            var settings = new OpenApiConvertSettings();
            ODataPathProvider provider = new ODataPathProvider();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(17313, paths.Count());
        }

        [Fact]
        public void GetPathsForGraphBetaModelWithDerivedTypesConstraintReturnsAllPaths()
        {
            // Arrange
            IEdmModel model = EdmModelHelper.GraphBetaModel;
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings
            {
                RequireDerivedTypesConstraintForBoundOperations = true
            };

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(13642, paths.Count());
        }

        [Fact]
        public void GetPathsForInheritanceModelWithoutDerivedTypesConstraintReturnsMore()
        {
            // Arrange
            IEdmModel model = GetInheritanceModel(string.Empty);
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(3, paths.Count());
        }

        [Fact]
        public void GetPathsForInheritanceModelWithDerivedTypesConstraintNoAnnotationReturnsFewer()
        {
            // Arrange
            IEdmModel model = GetInheritanceModel(string.Empty);
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings
            {
                RequireDerivedTypesConstraintForBoundOperations = true
            };

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(2, paths.Count());
        }

        [Fact]
        public void GetPathsForInheritanceModelWithDerivedTypesConstraintWithAnnotationReturnsMore()
        {
            // Arrange
            IEdmModel model = GetInheritanceModel(@"
<Annotation Term=""Org.OData.Validation.V1.DerivedTypeConstraint"">
<Collection>
  <String>NS.Customer</String>
  <String>NS.NiceCustomer</String>
</Collection>
</Annotation>");
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings
            {
                RequireDerivedTypesConstraintForBoundOperations = true
            };

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.Equal(3, paths.Count());
        }

#if DEBUG
        // Super useful for debugging tests.
        private string ListToString(IEnumerable<ODataPath> paths)
        {
            return string.Join(Environment.NewLine,
                paths.Select(p => string.Join("/", p.Segments.Select(s => s.Identifier))));
        }
#endif

        [Fact]
        public void GetPathsForNavPropModelWithoutDerivedTypesConstraintReturnsMore()
        {
            // Arrange
            IEdmModel model = GetNavPropModel(string.Empty);
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(4, paths.Count());
        }

        [Fact]
        public void GetPathsForNavPropModelWithDerivedTypesConstraintNoAnnotationReturnsFewer()
        {
            // Arrange
            IEdmModel model = GetNavPropModel(string.Empty);
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings
            {
                RequireDerivedTypesConstraintForBoundOperations = true
            };

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(3, paths.Count());
        }

        [Fact]
        public void GetPathsForNavPropModelWithDerivedTypesConstraintWithAnnotationReturnsMore()
        {
            // Arrange
            IEdmModel model = GetNavPropModel(@"
<Annotation Term=""Org.OData.Validation.V1.DerivedTypeConstraint"">
<Collection>
  <String>NS.Customer</String>
  <String>NS.NiceCustomer</String>
</Collection>
</Annotation>");
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings
            {
                RequireDerivedTypesConstraintForBoundOperations = true
            };

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(4, paths.Count());
        }

        [Fact]
        public void GetPathsForSingleEntitySetWorks()
        {
            // Arrange
            IEdmModel model = GetEdmModel("", "");
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(2, paths.Count());
            Assert.Equal(new[] { "/Customers", "/Customers({ID})" }, paths.Select(p => p.GetPathItemName()));
        }

        [Fact]
        public void GetPathsWithSingletonWorks()
        {
            // Arrange
            IEdmModel model = GetEdmModel("", @"<Singleton Name=""Me"" Type=""NS.Customer"" />");
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(3, paths.Count());
            Assert.Contains("/Me", paths.Select(p => p.GetPathItemName()));
        }

        [Fact]
        public void GetPathsWithBoundFunctionOperationWorks()
        {
            // Arrange
            string boundFunction =
@"<Function Name=""delta"" IsBound=""true"">
   <Parameter Name=""bindingParameter"" Type=""Collection(NS.Customer)"" />
     <ReturnType Type=""Collection(NS.Customer)"" />
</Function>";
            IEdmModel model = GetEdmModel(boundFunction, "");
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(3, paths.Count());
            Assert.Contains("/Customers/NS.delta()", paths.Select(p => p.GetPathItemName()));
        }

        [Fact]
        public void GetPathsWithBoundActionOperationWorks()
        {
            // Arrange
            string boundAction =
@"<Action Name=""renew"" IsBound=""true"">
   <Parameter Name=""bindingParameter"" Type=""NS.Customer"" />
     <ReturnType Type=""Edm.Boolean"" />
</Action>";
            IEdmModel model = GetEdmModel(boundAction, "");
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(3, paths.Count());
            Assert.Contains("/Customers({ID})/NS.renew", paths.Select(p => p.GetPathItemName()));
        }

        [Fact]
        public void GetPathsWithUnboundOperationImportWorks()
        {
            // Arrange
            string boundAction =
@"<Function Name=""GetNearestCustomers"">
   <ReturnType Type=""NS.Customer"" />
  </Function >
  <Action Name=""ResetDataSource"" />";

            string unbounds = @"
<FunctionImport Name=""GetNearestCustomers"" Function=""NS.GetNearestCustomers"" EntitySet=""Customers"" />
<ActionImport Name =""ResetDataSource"" Action =""NS.ResetDataSource"" />";
            IEdmModel model = GetEdmModel(boundAction, unbounds);

            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(4, paths.Count());
            Assert.Contains("/GetNearestCustomers()", paths.Select(p => p.GetPathItemName()));
            Assert.Contains("/ResetDataSource", paths.Select(p => p.GetPathItemName()));
        }

        [Fact]
        public void GetPathsWithFalseNavigabilityInNavigationRestrictionsAnnotationWorks()
        {
            // Arrange
            string entityType =
@"<EntityType Name=""Order"">
    <Key>
      <PropertyRef Name=""id"" />
    </Key>
    <NavigationProperty Name=""MultipleCustomers"" Type=""Collection(NS.Customer)"" />
    <NavigationProperty Name=""SingleCustomer"" Type=""NS.Customer"" >
        <Annotation Term=""Org.OData.Capabilities.V1.NavigationRestrictions"">
            <Record>
                <PropertyValue Property = ""Navigability"">
                    <EnumMember>Org.OData.Capabilities.V1.NavigationType/None</EnumMember>
                </PropertyValue>
            </Record>
        </Annotation>
     </NavigationProperty>
  </EntityType>";

            string entitySet = @"<EntitySet Name=""Orders"" EntityType=""NS.Order"" />";
            IEdmModel model = GetEdmModel(entityType, entitySet);

            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(6, paths.Count());

            var pathItems = paths.Select(p => p.GetPathItemName()).ToList();
            Assert.DoesNotContain("/Orders({id})/SingleCustomer", pathItems);
            Assert.DoesNotContain("/Orders({id})/SingleCustomer/$ref", pathItems);
        }

        [Fact]
        public void GetPathsWithFalseIndexabilityByKeyInNavigationRestrictionsAnnotationWorks()
        {
            // Arrange
            string entityType =
@"<EntityType Name=""Order"">
    <Key>
      <PropertyRef Name=""id"" />
    </Key>
    <NavigationProperty Name=""MultipleCustomers"" Type=""Collection(NS.Customer)"" ContainsTarget=""true"" >
        <Annotation Term=""Org.OData.Capabilities.V1.NavigationRestrictions"">
            <Record>
                <PropertyValue Property=""RestrictedProperties"" >
                    <Collection>
                        <Record>
                            <PropertyValue Property=""IndexableByKey"" Bool=""false"" />
                        </Record>
                    </Collection>
                </PropertyValue>
            </Record>
        </Annotation>
    </NavigationProperty>
    <NavigationProperty Name=""SingleCustomer"" Type=""NS.Customer"" />
  </EntityType>";

            string entitySet = @"<EntitySet Name=""Orders"" EntityType=""NS.Order"" />";
            IEdmModel model = GetEdmModel(entityType, entitySet);

            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(7, paths.Count());

            var pathItems = paths.Select(p => p.GetPathItemName()).ToList();
            Assert.DoesNotContain("/Orders({id})/MultipleCustomers({ID})", pathItems);
        }

        [Fact]
        public void GetPathsWithNonContainedNavigationPropertyWorks()
        {
            // Arrange
            string entityType =
@"<EntityType Name=""Order"">
    <Key>
      <PropertyRef Name=""id"" />
    </Key>
    <NavigationProperty Name=""MultipleCustomers"" Type=""Collection(NS.Customer)"" />
    <NavigationProperty Name=""SingleCustomer"" Type=""NS.Customer"" />
  </EntityType>";

            string entitySet = @"<EntitySet Name=""Orders"" EntityType=""NS.Order"" />";
            IEdmModel model = GetEdmModel(entityType, entitySet);

            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(8, paths.Count());

            var pathItems = paths.Select(p => p.GetPathItemName()).ToList();
            Assert.Contains("/Orders({id})/MultipleCustomers", pathItems);
            Assert.Contains("/Orders({id})/SingleCustomer", pathItems);
            Assert.Contains("/Orders({id})/SingleCustomer/$ref", pathItems);
            Assert.Contains("/Orders({id})/MultipleCustomers/$ref", pathItems);
        }

        [Fact]
        public void GetPathsWithContainedNavigationPropertyWorks()
        {
            // Arrange
            string entityType =
@"<EntityType Name=""Order"">
    <Key>
      <PropertyRef Name=""id"" />
    </Key>
    <NavigationProperty Name=""MultipleCustomers"" Type=""Collection(NS.Customer)"" ContainsTarget=""true"" />
    <NavigationProperty Name=""SingleCustomer"" Type=""NS.Customer"" ContainsTarget=""true"" />
  </EntityType>";

            string entitySet = @"<EntitySet Name=""Orders"" EntityType=""NS.Order"" />";
            IEdmModel model = GetEdmModel(entityType, entitySet);
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Equal(7, paths.Count());

            var pathItems = paths.Select(p => p.GetPathItemName()).ToList();
            Assert.Contains("/Orders({id})/MultipleCustomers", pathItems);
            Assert.Contains("/Orders({id})/MultipleCustomers({ID})", pathItems);
            Assert.Contains("/Orders({id})/SingleCustomer", pathItems);
        }

        [Theory]
        [InlineData(true, "logo")]
        [InlineData(false, "logo")]
        [InlineData(true, "content")]
        [InlineData(false, "content")]
        public void GetPathsWithStreamPropertyAndWithEntityHasStreamWorks(bool hasStream, string streamPropName)
        {
            // Arrange
            IEdmModel model = GetEdmModel(hasStream, streamPropName);
            ODataPathProvider provider = new ODataPathProvider();
            var settings = new OpenApiConvertSettings();
            const string TodosContentPath = "/todos({id})/content";
            const string TodosValuePath = "/todos({id})/$value";
            const string TodosLogoPath = "/todos({id})/logo";

            // Act
            var paths = provider.GetPaths(model, settings);

            // Assert
            Assert.NotNull(paths);
            Assert.Contains("/catalog/content", paths.Select(p => p.GetPathItemName()));
            Assert.Contains("/catalog/thumbnailPhoto", paths.Select(p => p.GetPathItemName()));
            Assert.Contains("/me/photo/$value", paths.Select(p => p.GetPathItemName()));

            if (streamPropName.Equals("logo"))
            {
                if (hasStream)
                {
                    Assert.Equal(12, paths.Count());
                    Assert.Contains(TodosValuePath, paths.Select(p => p.GetPathItemName()));
                    Assert.Contains(TodosLogoPath, paths.Select(p => p.GetPathItemName()));
                    Assert.DoesNotContain(TodosContentPath, paths.Select(p => p.GetPathItemName()));
                }
                else
                {
                    Assert.Equal(11, paths.Count());
                    Assert.Contains(TodosLogoPath, paths.Select(p => p.GetPathItemName()));
                    Assert.DoesNotContain(TodosContentPath, paths.Select(p => p.GetPathItemName()));
                    Assert.DoesNotContain(TodosValuePath, paths.Select(p => p.GetPathItemName()));
                }
            }
            else if (streamPropName.Equals("content"))
            {
                Assert.Equal(11, paths.Count());
                Assert.Contains(TodosContentPath, paths.Select(p => p.GetPathItemName()));
                Assert.DoesNotContain(TodosLogoPath, paths.Select(p => p.GetPathItemName()));
                Assert.DoesNotContain(TodosValuePath, paths.Select(p => p.GetPathItemName()));
            }
        }

        private static IEdmModel GetEdmModel(string schemaElement, string containerElement)
        {
            string template = $@"<?xml version=""1.0"" encoding=""utf-16""?>
<Schema Namespace=""NS"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
  <EntityType Name=""Customer"">
    <Key>
      <PropertyRef Name=""ID"" />
    </Key>
    <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
  </EntityType>
  {schemaElement}
  <EntityContainer Name =""Default"">
    <EntitySet Name=""Customers"" EntityType=""NS.Customer"" />
    {containerElement}
  </EntityContainer>
</Schema>";
            return GetEdmModel(template);
        }

        private static IEdmModel GetInheritanceModel(string annotation)
        {
            string template = $@"<?xml version=""1.0"" encoding=""utf-16""?>
<Schema Namespace=""NS"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
  <EntityType Name=""Customer"">
    <Key>
      <PropertyRef Name=""ID"" />
    </Key>
    <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
  </EntityType>
  <EntityType Name=""NiceCustomer"" BaseType=""NS.Customer"">
    <Property Name=""Other"" Type=""Edm.Int32"" Nullable=""true"" />
  </EntityType>
  <Action Name=""Ack"" IsBound=""true"" >
    <Parameter Name = ""bindingParameter"" Type=""NS.NiceCustomer"" />
  </Action>
  <EntityContainer Name =""Default"">
    <EntitySet Name=""Customers"" EntityType=""NS.Customer"">
      {annotation}
    </EntitySet>
  </EntityContainer>
</Schema>";
            return GetEdmModel(template);
        }

        private static IEdmModel GetNavPropModel(string annotation)
        {
            string template = $@"<?xml version=""1.0"" encoding=""utf-16""?>
<Schema Namespace=""NS"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
  <EntityType Name=""Root"">
    <Key>
      <PropertyRef Name=""ID"" />
    </Key>
    <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
    <NavigationProperty Name=""Customers"" Type=""Collection(NS.Customer)"" ContainsTarget=""true"">
      {annotation}
    </NavigationProperty>
  </EntityType>
  <EntityType Name=""Customer"">
    <Key>
      <PropertyRef Name=""ID"" />
    </Key>
    <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
  </EntityType>
  <EntityType Name=""NiceCustomer"" BaseType=""NS.Customer"">
    <Property Name=""Other"" Type=""Edm.Int32"" Nullable=""true"" />
  </EntityType>
  <Action Name=""Ack"" IsBound=""true"" >
    <Parameter Name = ""bindingParameter"" Type=""NS.NiceCustomer"" />
  </Action>
  <EntityContainer Name =""Default"">
    <Singleton Name=""Root"" Type=""NS.Root"" />
  </EntityContainer>
</Schema>";
            return GetEdmModel(template);
        }

        private static IEdmModel GetEdmModel(string schema)
        {
            bool parsed = SchemaReader.TryParse(new XmlReader[] { XmlReader.Create(new StringReader(schema)) }, out IEdmModel parsedModel, out IEnumerable<EdmError> errors);
            Assert.True(parsed, $"Parse failure. {string.Join(Environment.NewLine, errors.Select(e => e.ToString()))}");
            return parsedModel;
        }

        private static IEdmModel GetEdmModel(bool hasStream, string streamPropName)
        {
            string template = @"<edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
  <edmx:DataServices>
    <Schema Namespace=""microsoft.graph"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
      <EntityType Name=""todo"" HasStream=""{0}"">
        <Key>
          <PropertyRef Name=""id"" />
        </Key>
        <Property Name=""id"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""{1}"" Type=""Edm.Stream""/>
        <Property Name = ""description"" Type = ""Edm.String"" />
      </EntityType>
      <EntityType Name=""user"" OpenType=""true"">
        <NavigationProperty Name = ""photo"" Type = ""microsoft.graph.profilePhoto"" ContainsTarget = ""true"" />
      </EntityType>
      <EntityType Name=""profilePhoto"" HasStream=""true"">
        <Property Name = ""height"" Type = ""Edm.Int32"" />
        <Property Name = ""width"" Type = ""Edm.Int32"" />
      </EntityType >
      <EntityType Name=""document"">
        <Property Name=""content"" Type=""Edm.Stream""/>
        <Property Name=""thumbnailPhoto"" Type=""Edm.Stream""/>
      </EntityType>
      <EntityType Name=""catalog"" BaseType=""microsoft.graph.document"">
        <NavigationProperty Name=""reports"" Type = ""Collection(microsoft.graph.report)"" />
      </EntityType>
      <EntityContainer Name =""GraphService"">
        <EntitySet Name=""todos"" EntityType=""microsoft.graph.todo"" />
        <Singleton Name=""me"" Type=""microsoft.graph.user"" />
        <Singleton Name=""catalog"" Type=""microsoft.graph.catalog"" />
      </EntityContainer>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";
            string modelText = string.Format(template, hasStream, streamPropName);
            bool result = CsdlReader.TryParse(XElement.Parse(modelText).CreateReader(), out IEdmModel model, out _);
            Assert.True(result);
            return model;
        }
    }
}
