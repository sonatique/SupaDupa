using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SupaDupa.Core;

namespace SupaDupa.TestConsole
{
    static class Program
    {
        static async Task Main()
        {
			//var path = @"D:\Univers";
            var path = @"C:\Users\Me\Documents";

            var searcher = new DuplicateSearcher();

            var reporter = new Reporter();

			reporter.Run(CancellationToken.None);

            var result = await searcher.Search(
                new []{path},
                func =>
                {
	                reporter.ProgressGetter = func;
                }, 
                CancellationToken.None);

            Console.WriteLine($"Total file scanned: {result.TotalFileScanned}");
			Console.WriteLine($"Found {result.DuplicateGroups.Count()} duplicate groups");
			Console.WriteLine($"Found {result.DuplicateGroups.Sum(g => g.FileNames.Count -1)} duplicate files");
			Console.WriteLine($"Total duplicate size: {result.DuplicateGroups.Sum(g => g.FileSize * g.FileNames.Count -1) / (1024*1024)}");

			using var outFile = new StreamWriter(File.Create("Results.txt"));
			foreach (var resultDuplicateGroup in result.DuplicateGroups.ToImmutableSortedDictionary(g => g.FileSize, g => g.FileNames))
			{
				outFile.WriteLine($"{resultDuplicateGroup.Key} : {resultDuplicateGroup.Value.Count}");
				foreach (var fileName in resultDuplicateGroup.Value.ToImmutableSortedSet())
				{
					outFile.WriteLine($"{fileName}");
				}
				outFile.WriteLine();
			}
		}
    }

    class Reporter
    {
        public DuplicateSearcher.ProgressGetter ProgressGetter { get; set; }

        public async Task Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var progress = ProgressGetter?.Invoke();
                if (progress.HasValue)
                {
	                var progressReport = progress.Value;
	                Console.WriteLine(
		                $"Completed: {(progressReport.totalFileCount > 0 ? 100f * progressReport.processedFileCout / progressReport.totalFileCount : 0f):F2}" +
		                $" / Remaining: {progressReport.remainingDuration:g}" +
		                $" / Last processed: {progressReport.lastProcessedFile}");
                }

                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}
