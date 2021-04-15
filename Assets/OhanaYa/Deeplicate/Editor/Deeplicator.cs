using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

using System.Linq;
using System.Collections.Generic;

namespace OhanaYa
{
    using Array = System.Array;

    public static class Deeplicator
    {
        const string LogTag = "Deeplicate";
        const string MenuItemName = "Assets/Deeplicate %#d";

        struct CopyAssetPathPair
        {
            public string SourcePath;
            public string DestinationPath;
        }

        [MenuItem(MenuItemName)]
        static void Deeplicate()
        {
            AssetDatabase.Refresh();

            var selectedObjects = Selection.objects;
            var selectedAssetPaths = selectedObjects.Select(x => AssetDatabase.GetAssetPath(x));
            var pairs = new List<CopyAssetPathPair>();

            // Copy assets.
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in selectedAssetPaths)
                {
                    var newPath = AssetDatabase.GenerateUniqueAssetPath(path);

                    if (AssetDatabase.CopyAsset(path, newPath))
                    {
                        pairs.Add(new CopyAssetPathPair
                        {
                            SourcePath = path,
                            DestinationPath = newPath,
                        });
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Import copied assets.
            AssetDatabase.Refresh();

            // Replace object references.
            var sourceFolderPaths = selectedAssetPaths.Where(AssetDatabase.IsValidFolder);
            var sourceFilePaths = selectedAssetPaths.Except(sourceFolderPaths);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var pair in pairs)
                {
                    var src = pair.SourcePath;
                    var dst = pair.DestinationPath;
                    var isCopyFolder = AssetDatabase.IsValidFolder(src);

                    var copiedAssetPaths = isCopyFolder
                        ? AssetDatabase
                            .FindAssets(filter: "t:Object", searchInFolders: new[] { dst })
                            .Select(x => AssetDatabase.GUIDToAssetPath(x))
                            .Where(path => !AssetDatabase.IsValidFolder(path))
                        : new[] { dst };

                    foreach (var copiedAssetPath in copiedAssetPaths)
                    {
                        if (copiedAssetPath.EndsWith(".unity"))
                        {
                            Debug.unityLogger.LogWarning(
                                LogTag,
                                $"{copiedAssetPath} has been sharrow copied (SceneAsset is not supported).",
                                AssetDatabase.LoadAssetAtPath<SceneAsset>(copiedAssetPath));
                            continue;
                        }

                        var copiedSerializedObjects = AssetDatabase
                            .LoadAllAssetsAtPath(copiedAssetPath)
                            .Where(x => x != null) // Ignore broken assets.
                            .Select(x => new SerializedObject(x));

                        foreach (var copiedSerializedObject in copiedSerializedObjects)
                        {
                            var iterator = copiedSerializedObject.GetIterator();

                            // Include HideInInspector properties.
                            while (iterator.Next(enterChildren: true))
                            {
                                var targetProperty = iterator;

                                var propertyType = targetProperty.propertyType;
                                if (propertyType != SerializedPropertyType.ObjectReference)
                                {
                                    continue;
                                }

                                var objectReference = targetProperty.objectReferenceValue;
                                if (objectReference == null)
                                {
                                    continue;
                                }

                                var sourceAssetPath = AssetDatabase.GetAssetPath(objectReference);
                                var isFile = sourceFilePaths.Contains(sourceAssetPath);
                                var folders = sourceFolderPaths.Where(x => sourceAssetPath.StartsWith(x + "/"));
                                var isInFolder = folders.Any();
                                if (!(isFile || isInFolder))
                                {
                                    continue;
                                }

                                var destinationAssetPath = "";

                                if (isCopyFolder && sourceAssetPath.StartsWith(src + "/"))
                                {
                                    destinationAssetPath = dst + sourceAssetPath.Substring(src.Length);
                                }
                                else if (isCopyFolder && isInFolder)
                                {
                                    var srcFolders = sourceFolderPaths.Where(x => src.StartsWith(x + "/"));
                                    var commonFolders = folders.Intersect(srcFolders);
                                    var sourceFolderPath = (commonFolders.Any() ? commonFolders : folders).Min(); // Shortest match path
                                    var destinationFolderPath = pairs.Find(x => x.SourcePath == sourceFolderPath).DestinationPath;

                                    destinationAssetPath = destinationFolderPath + sourceAssetPath.Substring(sourceFolderPath.Length);
                                }
                                else
                                {
                                    if (isFile)
                                    {
                                        destinationAssetPath = pairs.Find(x => x.SourcePath == sourceAssetPath).DestinationPath;
                                    }
                                    else
                                    {
                                        var sourceFolderPath = folders.Max(); // Longest match path
                                        var destinationFolderPath = pairs.Find(x => x.SourcePath == sourceFolderPath).DestinationPath;

                                        destinationAssetPath = destinationFolderPath + sourceAssetPath.Substring(sourceFolderPath.Length);
                                    }
                                }
                                var destinationAsset = AssetDatabase.LoadAssetAtPath<Object>(destinationAssetPath);
                                if (destinationAsset == null)
                                {
                                    continue;
                                }

#if UNITY_2018_3_OR_NEWER
                                // Nested Prefabs
                                var isPrefabReference = targetProperty.type == "PPtr<Prefab>";
                                if (isPrefabReference)
                                {
                                    var pptr = new SerializedObject(targetProperty.objectReferenceValue);
                                    var m_RootGameObject = pptr.FindProperty("m_RootGameObject");
                                    Assert.IsNotNull(m_RootGameObject);
                                    Assert.AreEqual(m_RootGameObject.propertyType, SerializedPropertyType.ObjectReference);
                                    Assert.IsNotNull(m_RootGameObject.objectReferenceValue);
                                    objectReference = m_RootGameObject.objectReferenceValue;
                                }
#endif

                                var isMainAsset = AssetDatabase.IsMainAsset(objectReference);

                                if (isMainAsset)
                                {
                                    targetProperty.objectReferenceValue =
#if UNITY_2018_3_OR_NEWER
                                        isPrefabReference
#pragma warning disable 0618
                                            //FIXME: GetPrefabObject is obsolete.
                                            ? PrefabUtility.GetPrefabObject(destinationAsset) :
#pragma warning restore 0618
#endif
                                            destinationAsset;
                                }
                                else
                                {
                                    var sourceType = objectReference.GetType();
                                    var sourceName = objectReference.name;

                                    var destinationSubAssets = AssetDatabase
                                        .LoadAllAssetsAtPath(destinationAssetPath)
                                        .Where(x => !AssetDatabase.IsMainAsset(x)) // Attached Component is not SubAsset.
                                        .Where(x => x.GetType() == sourceType);

                                    var nameMatches = destinationSubAssets.Where(x => x.name == sourceName);
                                    var nameMatchCount = nameMatches.Count();
                                    Assert.IsTrue(nameMatchCount > 0);
                                    if (nameMatchCount == 1)
                                    {
                                        targetProperty.objectReferenceValue = nameMatches.First();
                                    }
                                    else
                                    {
                                        var sourceMainAsset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GetAssetPath(objectReference));
                                        Assert.IsNotNull(sourceMainAsset);

                                        var sourceAncestors = ProjectWindowUtil.GetAncestors(objectReference.GetInstanceID())
                                            .Except(ProjectWindowUtil.GetAncestors(sourceMainAsset.GetInstanceID()));
                                        var sourceAncestorObjects = sourceAncestors.Select(x => EditorUtility.InstanceIDToObject(x));
                                        var sourceFullName = string.Format(
                                            "{0}/{1}",
                                            sourceAncestorObjects
                                                .Reverse()
                                                .Select(x => x.name)
                                                .Aggregate((sum, x) => string.Format("{0}/{1}", sum, x)),
                                            sourceName);
                                        var destinationAncestors = ProjectWindowUtil.GetAncestors(destinationAsset.GetInstanceID());

                                        var fullNameMatches = destinationSubAssets
                                            .Select(asset => new
                                            {
                                                asset,
                                                fullName = string.Format(
                                                    "{0}/{1}",
                                                    ProjectWindowUtil
                                                        .GetAncestors(asset.GetInstanceID())
                                                        .Except(destinationAncestors)
                                                        .Select(x => EditorUtility.InstanceIDToObject(x))
                                                        .Reverse()
                                                        .Select(x => x.name)
                                                        .Aggregate((sum, x) => string.Format("{0}/{1}", sum, x)),
                                                    asset.name)
                                            })
                                            .Where(x => x.fullName == sourceFullName);
                                        var fullNameMatchCount = fullNameMatches.Count();
                                        Assert.IsTrue(fullNameMatchCount > 0);
                                        if (fullNameMatchCount == 1)
                                        {
                                            targetProperty.objectReferenceValue = fullNameMatches.First().asset;
                                        }
                                        else
                                        {
                                            if (objectReference is GameObject)
                                            {
                                                var destinationGameObjects = fullNameMatches
                                                    .Select(x => x.asset)
                                                    .Cast<GameObject>();

                                                var gameObjectReference = (GameObject)objectReference;
                                                var sourceSiblingIndices = gameObjectReference
                                                    .GetComponentsInParent<Transform>(includeInactive: true)
                                                    .Select(x => x.GetSiblingIndex());

                                                var destinationGameObject = destinationGameObjects
                                                    .First(x => x
                                                        .GetComponentsInParent<Transform>()
                                                        .Select(t => t.GetSiblingIndex())
                                                        .SequenceEqual(sourceSiblingIndices));
                                                Assert.IsNotNull(destinationGameObject);

                                                targetProperty.objectReferenceValue = destinationGameObject;
                                            }
                                            else if (objectReference is Component)
                                            {
                                                var destinationGameObjects = fullNameMatches
                                                    .Select(x => x.asset)
                                                    .Cast<Component>()
                                                    .Select(x => x.gameObject);

                                                var componentReference = (Component)objectReference;
                                                var sourceSiblingIndices = componentReference
                                                    .GetComponentsInParent<Transform>(includeInactive: true)
                                                    .Select(x => x.GetSiblingIndex());
                                                var sourceComponentIndex = Array.IndexOf(componentReference.gameObject.GetComponents(sourceType), componentReference);
                                                Assert.IsTrue(sourceComponentIndex >= 0);

                                                var destinationGameObject = destinationGameObjects
                                                    .First(x => x
                                                        .GetComponentsInParent<Transform>()
                                                        .Select(t => t.GetSiblingIndex())
                                                        .SequenceEqual(sourceSiblingIndices));
                                                Assert.IsNotNull(destinationGameObject);

                                                var destinationComponent = destinationGameObject.GetComponents(sourceType)[sourceComponentIndex];
                                                Assert.IsNotNull(destinationComponent);

                                                targetProperty.objectReferenceValue = destinationComponent;
                                            }
                                            else
                                            {
                                                // Other assets.

                                                // TODO
                                                Debug.unityLogger.LogWarning(LogTag, $"Ambiguous {objectReference.GetType().FullName} is not supported.");
                                            }
                                        }
                                    }
                                }
                            }
                            copiedSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Save changes.
            AssetDatabase.SaveAssets();

            Selection.objects = pairs
                .Select(p => p.DestinationPath)
                .Select(x => AssetDatabase.LoadAssetAtPath<Object>(x))
                .ToArray();
        }

        [MenuItem(MenuItemName, true)]
        static bool ValidateDeeplicate()
        {
            return Selection.objects.All(AssetDatabase.Contains);
        }
    }
}
