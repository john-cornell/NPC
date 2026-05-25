using System;
using System.Collections.Generic;
using System.Linq;

namespace NPC.Library.Decision
{
    public class GeneticEvolutionManager
    {
        public int CurrentGeneration { get; private set; } = 1;
        
        private readonly int _populationSize;
        private readonly float _mutationRate;
        private readonly float _mutationAmount;
        private readonly int _elitismCount;

        // Keep track of the best network across generations
        public NeuralNetwork BestEverNetwork { get; private set; }
        public float BestEverFitness { get; private set; } = float.MinValue;

        public GeneticEvolutionManager(int populationSize, float mutationRate = 0.1f, float mutationAmount = 0.5f, float elitismRatio = 0.2f)
        {
            _populationSize = populationSize;
            _mutationRate = mutationRate;
            _mutationAmount = mutationAmount;
            _elitismCount = Math.Max(1, (int)(populationSize * elitismRatio));
        }

        /// <summary>
        /// Takes the current generation of networks and their associated fitness scores,
        /// and returns a new generation of networks through elitism and mutation.
        /// </summary>
        public List<NeuralNetwork> Evolve(List<(NeuralNetwork Brain, float Fitness)> population)
        {
            // Sort by fitness descending (highest fitness first)
            var sortedPopulation = population.OrderByDescending(p => p.Fitness).ToList();
            
            // Track all-time best
            if (sortedPopulation[0].Fitness > BestEverFitness)
            {
                BestEverFitness = sortedPopulation[0].Fitness;
                BestEverNetwork = sortedPopulation[0].Brain.Clone();
            }

            var nextGeneration = new List<NeuralNetwork>();

            // Elitism: The top performing networks survive directly to the next generation without mutation
            var elites = sortedPopulation.Take(_elitismCount).Select(p => p.Brain).ToList();
            
            // Add exact clones of elites to preserve perfect behavior
            foreach (var elite in elites)
            {
                nextGeneration.Add(elite.Clone());
            }

            // Fill the rest of the population with mutated children from two parents
            Random random = new Random();
            while (nextGeneration.Count < _populationSize)
            {
                // Randomly select two distinct parents from the elites
                var parentA = elites[random.Next(elites.Count)];
                var parentB = elites[random.Next(elites.Count)];
                
                // Breed them via Crossover
                var child = parentA.Crossover(parentB);
                
                // Mutate the child
                child.Mutate(_mutationRate, _mutationAmount);
                
                nextGeneration.Add(child);
            }

            CurrentGeneration++;
            return nextGeneration;
        }
    }
}
