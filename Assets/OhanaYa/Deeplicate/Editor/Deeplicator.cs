using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

using System.Linq;
using System.Collections.Generic;

namespace OhanaYa.Deeplicate
{
    public static class Deeplicator
    {
        struct CopyAssetPathPair
        {
            public string SourcePath;
            public string DestinationPath;
        }

        [MenuItem("Assets/Deeplicate %#d")]
        static void Deeplicate()
        {
            var selecteds = Selection.objects;
            var selectedPaths = selecteds.Select(x => AssetDatabase.GetAssetPath(x));
            var pairs = new List<CopyAssetPathPair>();
            var sources = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);

                // Copy assets.
                foreach (var path in selectedPaths)
                {
                    var newPath = AssetDatabase.GenerateUniqueAssetPath(path);

                    if (AssetDatabase.CopyAsset(path, newPath))
                    {
                        pairs.Add(new CopyAssetPathPair{
                            SourcePath = path,
                            DestinationPath = newPath});
                    }
                    else
                    {
                        Debug.LogError("Failed to copy file: " + path);
                    }

                }

                var destinationPaths = pairs.Select(p => p.DestinationPath);

                // Import copied assets.
                foreach (var path in destinationPaths)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive);
                }

                // Find references.
                foreach (var pair in pairs.Where(p => AssetDatabase.IsValidFolder(p.SourcePath)))
                {
                    var src = pair.SourcePath;
                    var dst = pair.DestinationPath;

                    var copiedObjectPaths = AssetDatabase
                        .FindAssets(filter: "t:Object", searchInFolders: new string[]{dst})
                        .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                        .Where(path => !AssetDatabase.IsValidFolder(path));

                    // Replace references.
                    foreach (var path in copiedObjectPaths)
                    {
                        var copiedObject = AssetDatabase.LoadAssetAtPath<Object>(path);

                        var serializedObjects = (copiedObject is GameObject)
                            ? ((GameObject)copiedObject)
                                .GetComponentsInChildren<Component>(includeInactive: true)
                                .Select(x => new SerializedObject(x))
                                .ToArray()
                            : new SerializedObject[]{new SerializedObject(copiedObject)};

                        foreach (var serializedObject in serializedObjects)
                        {
                            var iterator = serializedObject.GetIterator();

                            while (iterator.NextVisible(enterChildren: true))
                            {
                                var type = iterator.propertyType;
                                if (type != SerializedPropertyType.ObjectReference) continue;

                                var objectReference = iterator.objectReferenceValue;
                                if (objectReference == null || !sources.Contains(objectReference)) continue;

                                var sourcePath = AssetDatabase.GetAssetPath(objectReference);
                                Assert.IsTrue(sourcePath.StartsWith(src));

                                var destinationPath = dst + sourcePath.Substring(src.Length);
                                var copiedObjectReference = AssetDatabase.LoadAssetAtPath<Object>(destinationPath);
                                Assert.IsNotNull(copiedObjectReference);

                                iterator.objectReferenceValue = copiedObjectReference;
                            }

                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                }

                // Save changes.
                AssetDatabase.SaveAssets();

                Selection.objects = destinationPaths.Select(path => AssetDatabase.LoadAssetAtPath<Object>(path)).ToArray();
        }

        [MenuItem("Assets/Deeplicate %#d", true)]
        static bool ValidateDeeplicate()
        {
            return Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
        }
    }
}
