using System;
using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using FF = Unity.InferenceEngine.Functional;

namespace MRStainCleaning.Detection.Editor
{
    public static class StainOnnxToSentisConverter
    {
        private const string OnnxAssetPath = "Assets/Project/Models/YOLO/stain_det_v2.onnx";
        private const string OutputSentisPath = "Assets/Project/Models/YOLO/stain_det_v2_xywh.sentis";
        private const string LabelsAssetPath = "Assets/Project/Models/YOLO/stain_det_v2_labels.txt";
        private const string DefaultLabel = "stain";

        [MenuItem("Tools/Stain Detection/Convert ONNX To Sentis")]
        public static void Convert()
        {
            var onnxAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(OnnxAssetPath);
            if (onnxAsset == null)
            {
                EditorUtility.DisplayDialog("ONNX Missing", $"Could not find ONNX asset at '{OnnxAssetPath}'.", "OK");
                return;
            }

            try
            {
                EnsureLabelFile();

                var finalModel = BuildProviderCompatibleModel(onnxAsset);
                SaveSentisModel(finalModel);

                AssetDatabase.ImportAsset(OutputSentisPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(LabelsAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Conversion Complete",
                    $"Created '{OutputSentisPath}' and confirmed '{LabelsAssetPath}'.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Conversion Failed", ex.Message, "OK");
            }
        }

        private static Model BuildProviderCompatibleModel(ModelAsset onnxAsset)
        {
            var sourceModel = ModelLoader.Load(onnxAsset);
            if (sourceModel.outputs.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected a single YOLO-style output tensor, but found {sourceModel.outputs.Count} outputs.");
            }

            var graph = new FunctionalGraph();
            var inputs = graph.AddInputs(sourceModel);
            var raw = FF.Forward(sourceModel, inputs)[0];

            var boxes = raw[0, 0..4, ..].Transpose(0, 1);
            var classScores = raw[0, 4.., ..].Transpose(0, 1);
            var scores = FF.ReduceMax(classScores, 1);
            var ids = FF.ArgMax(classScores, 1);

            return graph.Compile(boxes, ids, scores);
        }

        private static void SaveSentisModel(Model model)
        {
            var absoluteOutputPath = ResolveProjectPath(OutputSentisPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException($"Could not resolve output directory for '{OutputSentisPath}'.");
            }

            Directory.CreateDirectory(outputDirectory);
            ModelWriter.Save(OutputSentisPath, model);
        }

        private static void EnsureLabelFile()
        {
            var absoluteLabelsPath = ResolveProjectPath(LabelsAssetPath);
            var labelsDirectory = Path.GetDirectoryName(absoluteLabelsPath);
            if (string.IsNullOrEmpty(labelsDirectory))
            {
                throw new InvalidOperationException($"Could not resolve label directory for '{LabelsAssetPath}'.");
            }

            Directory.CreateDirectory(labelsDirectory);

            if (!File.Exists(absoluteLabelsPath) || string.IsNullOrWhiteSpace(File.ReadAllText(absoluteLabelsPath)))
            {
                File.WriteAllText(absoluteLabelsPath, DefaultLabel + Environment.NewLine);
            }
        }

        private static string ResolveProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Could not resolve the Unity project root.");
            }

            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
