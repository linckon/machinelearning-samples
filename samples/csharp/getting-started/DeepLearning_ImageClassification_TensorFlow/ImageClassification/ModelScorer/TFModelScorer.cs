﻿using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.ML.Transforms;
using Microsoft.ML;

using ImageClassification.ImageDataStructures;
using static ImageClassification.ModelScorer.ConsoleHelpers;
using static ImageClassification.ModelScorer.ModelHelpers;
using Microsoft.ML.Data;
using Microsoft.ML.ImageAnalytics;
using Microsoft.ML.Transforms.TensorFlow;

namespace ImageClassification.ModelScorer
{
    public class TFModelScorer
    {
        private readonly string dataLocation;
        private readonly string imagesFolder;
        private readonly string modelLocation;
        private readonly string labelsLocation;
        private readonly MLContext mlContext;
        private static string ImageReal = nameof(ImageReal);

        public TFModelScorer(string dataLocation, string imagesFolder, string modelLocation, string labelsLocation)
        {
            this.dataLocation = dataLocation;
            this.imagesFolder = imagesFolder;
            this.modelLocation = modelLocation;
            this.labelsLocation = labelsLocation;
            mlContext = new MLContext();
        }

        public struct ImageNetSettings
        {
            public const int imageHeight = 224;
            public const int imageWidth = 224;
            public const float mean = 117;
            public const bool channelsLast = true;
        }

        public struct InceptionSettings
        {
            // for checking tensor names, you can use tools like Netron,
            // which is installed by Visual Studio AI Tools

            // input tensor name
            public const string inputTensorName = "input";

            // output tensor name
            public const string outputTensorName = "softmax2";
        }

        public void Score()
        {
            var model = LoadModel(dataLocation, imagesFolder, modelLocation);

            var predictions = PredictDataUsingModel(dataLocation, imagesFolder, labelsLocation, model).ToArray();

        }

        private PredictionEngine<ImageNetData, ImageNetPrediction> LoadModel(string dataLocation, string imagesFolder, string modelLocation)
        {
            ConsoleWriteHeader("Read model");
            Console.WriteLine($"Model location: {modelLocation}");
            Console.WriteLine($"Images folder: {imagesFolder}");
            Console.WriteLine($"Training file: {dataLocation}");
            Console.WriteLine($"Default parameters: image size=({ImageNetSettings.imageWidth},{ImageNetSettings.imageHeight}), image mean: {ImageNetSettings.mean}");

            var data = mlContext.Data.LoadFromTextFile<ImageNetData>(dataLocation, hasHeader: true);

            //// Temp debugging code ////
            var modelTF = TensorFlowUtils.LoadTensorFlowModel(mlContext, modelLocation);
            var schema = modelTF.GetInputSchema();
            /////////////////////////////

            var pipeline = mlContext.Transforms.LoadImages(imageFolder: imagesFolder, columnPairs: (outputColumnName: ImageReal, inputColumnName: nameof(ImageNetData.ImagePath)))
                            .Append(mlContext.Transforms.ResizeImages(outputColumnName: ImageReal, imageWidth: ImageNetSettings.imageWidth, imageHeight: ImageNetSettings.imageHeight, inputColumnName: ImageReal))
                            .Append(mlContext.Transforms.ExtractPixels(columns: new[] { new ImagePixelExtractingEstimator.ColumnOptions(name: InceptionSettings.inputTensorName, inputColumnName: ImageReal, interleave: ImageNetSettings.channelsLast, offset: ImageNetSettings.mean) }))
                            .Append(mlContext.Transforms.ScoreTensorFlowModel(modelLocation:modelLocation, outputColumnNames:new[] { InceptionSettings.outputTensorName }, inputColumnNames: new[] { InceptionSettings.inputTensorName } ));
            var model = pipeline.Fit(data);

            var predictionEngine = model.CreatePredictionEngine<ImageNetData, ImageNetPrediction>(mlContext);

            return predictionEngine;
        }

        protected IEnumerable<ImageNetData> PredictDataUsingModel(string testLocation, 
                                                                  string imagesFolder, 
                                                                  string labelsLocation, 
                                                                  PredictionEngine<ImageNetData, ImageNetPrediction> model)
        {
            ConsoleWriteHeader("Classificate images");
            Console.WriteLine($"Images folder: {imagesFolder}");
            Console.WriteLine($"Training file: {testLocation}");
            Console.WriteLine($"Labels file: {labelsLocation}");

            var labels = ModelHelpers.ReadLabels(labelsLocation);

            var testData = ImageNetData.ReadFromCsv(testLocation, imagesFolder);

            foreach (var sample in testData)
            {
                var probs = model.Predict(sample).PredictedLabels;
                var imageData = new ImageNetDataProbability()
                {
                    ImagePath = sample.ImagePath,
                    Label = sample.Label
                };
                (imageData.PredictedLabel, imageData.Probability) = GetBestLabel(labels, probs);
                imageData.ConsoleWrite();
                yield return imageData;
            }
        }
    }
}
