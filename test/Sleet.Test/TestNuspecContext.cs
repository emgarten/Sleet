using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Sleet.Test
{
    public class TestNuspecContext
    {
        public string Id { get; set; } = "a";
        public string Version { get; set; } = "1.0.0";
        public string MinClientVersion { get; set; }
        public string Title { get; set; }
        public string Authors { get; set; }
        public string Owners { get; set; }
        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public string Summary { get; set; }
        public string Language { get; set; }
        public string ProjectUrl { get; set; }
        public string IconUrl { get; set; }
        public string LicenseUrl { get; set; }
        public string Copyright { get; set; }
        public string RequireLicenseAcceptance { get; set; }
        public string Tags { get; set; }
        public string DevelopmentDependency { get; set; }
        public List<PackageDependencyGroup> Dependencies { get; set; } = new List<PackageDependencyGroup>();
        public List<KeyValuePair<string, List<NuGetFramework>>> FrameworkAssemblies { get; set; } = new List<KeyValuePair<string, List<NuGetFramework>>>();
        public List<ContentFilesEntry> ContentFiles { get; set; } = new List<ContentFilesEntry>();

        public XDocument Create()
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "no"));
            var package = new XElement(XName.Get("package"));
            var metadata = new XElement(XName.Get("metadata"));
            package.Add(metadata);
            doc.Add(package);

            metadata.Add(new XElement(XName.Get("id"), Id));
            metadata.Add(new XElement(XName.Get("version"), Version));

            if (!string.IsNullOrEmpty(MinClientVersion))
            {
                metadata.Add(new XAttribute(XName.Get("minClientVersion"), MinClientVersion));
            }

            AddIfExists(metadata, "title", Title);
            AddIfExists(metadata, "authors", Authors);
            AddIfExists(metadata, "owners", Owners);
            AddIfExists(metadata, "description", Description);
            AddIfExists(metadata, "releaseNotes", ReleaseNotes);
            AddIfExists(metadata, "summary", Summary);
            AddIfExists(metadata, "language", Language);
            AddIfExists(metadata, "projectUrl", ProjectUrl);
            AddIfExists(metadata, "iconUrl", IconUrl);
            AddIfExists(metadata, "licenseUrl", LicenseUrl);
            AddIfExists(metadata, "copyright", Copyright);
            AddIfExists(metadata, "requireLicenseAcceptance", RequireLicenseAcceptance);
            AddIfExists(metadata, "tags", Tags);
            AddIfExists(metadata, "developmentDependency", DevelopmentDependency);

            if (Dependencies.Any())
            {
                var dependencies = new XElement(XName.Get("dependencies"));
                metadata.Add(dependencies);

                if (Dependencies.All(d => d.TargetFramework.IsAny))
                {
                    foreach (var d in Dependencies.Single().Packages)
                    {
                        var dependency = new XElement(XName.Get("dependency"));

                        dependency.Add(new XAttribute(XName.Get("id"), d.Id));
                        dependency.Add(new XAttribute(XName.Get("version"), d.VersionRange.ToLegacyShortString()));

                        dependencies.Add(dependency);
                    }
                }
                else
                {
                    foreach (var group in Dependencies)
                    {
                        var groupNode = new XElement(XName.Get("group"));
                        dependencies.Add(groupNode);

                        if (!group.TargetFramework.IsAny)
                        {
                            groupNode.Add(new XAttribute(XName.Get("targetFramework"), group.TargetFramework.GetShortFolderName()));
                        }

                        foreach (var d in group.Packages)
                        {
                            var dependency = new XElement(XName.Get("dependency"));

                            dependency.Add(new XAttribute(XName.Get("id"), d.Id));
                            dependency.Add(new XAttribute(XName.Get("version"), d.VersionRange.ToLegacyShortString()));

                            groupNode.Add(dependency);
                        }
                    }
                }
            }

            if (FrameworkAssemblies.Any())
            {
                var frameworkAssemblies = new XElement(XName.Get("frameworkAssemblies"));
                metadata.Add(frameworkAssemblies);

                foreach (var fwa in FrameworkAssemblies)
                {
                    var fwaNode = new XElement(XName.Get("frameworkAssembly"));
                    frameworkAssemblies.Add(fwaNode);
                    fwaNode.Add(new XAttribute("assemblyName", fwa.Key));
                    fwaNode.Add(new XAttribute("targetFramework", string.Join(",", fwa.Value.Select(f => f.GetShortFolderName()))));
                }
            }

            return doc;
        }

        private static void AddIfExists(XElement root, string elementName, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                root.Add(new XElement(XName.Get(elementName), value));
            }
        }
    }
}
