using System;
using System.Text.Json.Serialization;

namespace NPC.Library.Decision
{
    public class NeuralNetwork
    {
        public int[] Layers { get; set; }
        public float[][] Neurons { get; set; }
        public float[][] Biases { get; set; }
        public float[][][] Weights { get; set; }
        
        [JsonIgnore]
        private static readonly Random _random = new Random();

        // Parameterless constructor for deserialization
        public NeuralNetwork() { }

        public NeuralNetwork(int[] layers)
        {
            Layers = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                Layers[i] = layers[i];
            }
            InitNeurons();
            InitBiases();
            InitWeights();
        }

        private void InitNeurons()
        {
            Neurons = new float[Layers.Length][];
            for (int i = 0; i < Layers.Length; i++)
            {
                Neurons[i] = new float[Layers[i]];
            }
        }

        private void InitBiases()
        {
            Biases = new float[Layers.Length][];
            for (int i = 0; i < Layers.Length; i++)
            {
                Biases[i] = new float[Layers[i]];
                for (int j = 0; j < Layers[i]; j++)
                {
                    Biases[i][j] = (float)(_random.NextDouble() * 2 - 1); // -1 to 1
                }
            }
        }

        private void InitWeights()
        {
            Weights = new float[Layers.Length - 1][][];
            for (int i = 0; i < Layers.Length - 1; i++)
            {
                Weights[i] = new float[Layers[i + 1]][];
                for (int j = 0; j < Layers[i + 1]; j++)
                {
                    Weights[i][j] = new float[Layers[i]];
                    for (int k = 0; k < Layers[i]; k++)
                    {
                        Weights[i][j][k] = (float)(_random.NextDouble() * 2 - 1); // -1 to 1
                    }
                }
            }
        }

        public float[] FeedForward(float[] inputs)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                Neurons[0][i] = inputs[i];
            }

            for (int i = 1; i < Layers.Length; i++)
            {
                bool isOutputLayer = (i == Layers.Length - 1);
                for (int j = 0; j < Layers[i]; j++)
                {
                    float value = Biases[i][j];
                    for (int k = 0; k < Layers[i - 1]; k++)
                    {
                        value += Weights[i - 1][j][k] * Neurons[i - 1][k];
                    }
                    
                    if (isOutputLayer)
                    {
                        // Sigmoid for output layer (0.0 to 1.0)
                        Neurons[i][j] = 1.0f / (1.0f + (float)Math.Exp(-value));
                    }
                    else
                    {
                        // Tanh for hidden layers (-1.0 to 1.0)
                        Neurons[i][j] = (float)Math.Tanh(value);
                    }
                }
            }

            return Neurons[Layers.Length - 1];
        }

        public void Mutate(float mutationRate = 0.1f, float mutationAmount = 0.5f)
        {
            for (int i = 0; i < Biases.Length; i++)
            {
                for (int j = 0; j < Biases[i].Length; j++)
                {
                    if (_random.NextDouble() <= mutationRate)
                    {
                        Biases[i][j] += GetGaussianRandom() * mutationAmount;
                    }
                }
            }

            for (int i = 0; i < Weights.Length; i++)
            {
                for (int j = 0; j < Weights[i].Length; j++)
                {
                    for (int k = 0; k < Weights[i][j].Length; k++)
                    {
                        if (_random.NextDouble() <= mutationRate)
                        {
                            Weights[i][j][k] += GetGaussianRandom() * mutationAmount;
                        }
                    }
                }
            }
        }

        private float GetGaussianRandom()
        {
            double u1 = 1.0 - _random.NextDouble(); // uniform(0,1]
            double u2 = 1.0 - _random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return (float)randStdNormal;
        }

        public NeuralNetwork Clone()
        {
            NeuralNetwork clone = new NeuralNetwork(Layers);

            for (int i = 0; i < Biases.Length; i++)
            {
                for (int j = 0; j < Biases[i].Length; j++)
                {
                    clone.Biases[i][j] = Biases[i][j];
                }
            }

            for (int i = 0; i < Weights.Length; i++)
            {
                for (int j = 0; j < Weights[i].Length; j++)
                {
                    for (int k = 0; k < Weights[i][j].Length; k++)
                    {
                        clone.Weights[i][j][k] = Weights[i][j][k];
                    }
                }
            }

            return clone;
        }

        public NeuralNetwork Crossover(NeuralNetwork other)
        {
            NeuralNetwork child = new NeuralNetwork(Layers);

            for (int i = 0; i < Biases.Length; i++)
            {
                for (int j = 0; j < Biases[i].Length; j++)
                {
                    child.Biases[i][j] = _random.NextDouble() > 0.5 ? Biases[i][j] : other.Biases[i][j];
                }
            }

            for (int i = 0; i < Weights.Length; i++)
            {
                for (int j = 0; j < Weights[i].Length; j++)
                {
                    for (int k = 0; k < Weights[i][j].Length; k++)
                    {
                        child.Weights[i][j][k] = _random.NextDouble() > 0.5 ? Weights[i][j][k] : other.Weights[i][j][k];
                    }
                }
            }

            return child;
        }
    }
}
