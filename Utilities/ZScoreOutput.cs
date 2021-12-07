using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public class ZScoreOutput
    {
        public List<double> input;
        public List<int> signals;
        public List<double> avgFilter;
        public List<double> filtered_stddev;
    }

    public static class ZScore
    {
        public static ZScoreOutput StartAlgo(List<double> input, int lag, double threshold, double influence)
        {
            // init variables!
            int[] signals = new int[input.Count];
            double[] filteredY = new List<double>(input).ToArray();
            double[] avgFilter = new double[input.Count];
            double[] stdFilter = new double[input.Count];

            var initialWindow = new List<double>(filteredY).Skip(0).Take(lag).ToList();

            avgFilter[lag - 1] = Mean(initialWindow);
            stdFilter[lag - 1] = StdDev(initialWindow);

            for (int i = lag; i < input.Count; i++)
            {
                if (Math.Abs(input[i] - avgFilter[i - 1]) > threshold * stdFilter[i - 1])
                {
                    signals[i] = (input[i] > avgFilter[i - 1]) ? 1 : -1;
                    filteredY[i] = influence * input[i] + (1 - influence) * filteredY[i - 1];
                }
                else
                {
                    signals[i] = 0;
                    filteredY[i] = input[i];
                }

                // Update rolling average and deviation
                var slidingWindow = new List<double>(filteredY).Skip(i - lag).Take(lag + 1).ToList();

                var tmpMean = Mean(slidingWindow);
                var tmpStdDev = StdDev(slidingWindow);

                avgFilter[i] = Mean(slidingWindow);
                stdFilter[i] = StdDev(slidingWindow);
            }

            // Copy to convenience class 
            var result = new ZScoreOutput();
            result.input = input;
            result.avgFilter = new List<double>(avgFilter);
            result.signals = new List<int>(signals);
            result.filtered_stddev = new List<double>(stdFilter);

            return result;
        }

        private static double Mean(List<double> list)
        {
            // Simple helper function! 
            return list.Average();
        }

        private static double StdDev(List<double> values)
        {
            double ret = 0;
            if (values.Count() > 0)
            {
                double avg = values.Average();
                double sum = values.Sum(d => Math.Pow(d - avg, 2));
                ret = Math.Sqrt((sum) / (values.Count() - 1));
            }
            return ret;
        }
    }
}
