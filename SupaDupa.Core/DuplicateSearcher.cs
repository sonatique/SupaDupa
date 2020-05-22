using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SupaDupa.Core
{
    public readonly struct DuplicateSearcher
    {
	    public delegate (TimeSpan remainingDuration, int processedFileCout, int totalFileCount, string lastProcessedFile) ProgressGetter();
	
	    public async Task<Result> Search(IEnumerable<string> rootPaths, Action<ProgressGetter> setProgressGetter, CancellationToken cancel)
        {
            var sizeDico = new ConcurrentDictionary<long, ConcurrentBag<ConcurrentBag<string>>>();

            var totalNumberOfFiles = 0;

            var currentFileNumber = 0;

            var lastProcessedFile = string.Empty;

            long totalByteCount = 0;
            long currentByteCount = 0;
            
			var actionBlock = new ActionBlock<FileInfo[]>(fileInfos =>
            {
                for(var i = 0; i < fileInfos.Length; ++i)
                {
	                var fileInfo = fileInfos[i];
	                var length = fileInfo.Length;
	                var fullName = fileInfo.FullName;
	                lastProcessedFile = fullName;

					sizeDico.AddOrUpdate(length, 
		                _ => new ConcurrentBag<ConcurrentBag<string>> {new ConcurrentBag<string> {fullName}}, 
		                (_, bagbag) =>
		                {
			                foreach (var bag in bagbag)
			                {
				                if(FileCompare(fullName, bag.First()))
				                {
                                    bag.Add(fullName);
                                    return bagbag;
				                }
			                }

			                bagbag.Add(new ConcurrentBag<string> {fullName});
							
			                return bagbag;
		                });

	                ++currentFileNumber;
	                Interlocked.Add(ref currentByteCount, length);

	                if (cancel.IsCancellationRequested)
	                {
		                return;
	                }
                }
            }, new ExecutionDataflowBlockOptions
			{
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancel,
                EnsureOrdered = false,
                SingleProducerConstrained = true,
				BoundedCapacity = 30000
			});

			var startTime = DateTime.Now;
			
            setProgressGetter(() => (
	            currentByteCount/(1024*1024) > 0
		            ? ((DateTime.Now - startTime) / (currentByteCount/(1024*1024))) * ((totalByteCount - currentByteCount)/(1024*1024)) : default, 
	            currentFileNumber, totalNumberOfFiles, lastProcessedFile));

			foreach (var fis in FileAndPathUtils.RecurseEnumerateFileBlock(rootPaths))
            {
	            //actionBlock.Post(fis);
	            await actionBlock.SendAsync(fis, cancel);
	            totalNumberOfFiles += fis.Length;
	            totalByteCount += fis.Sum(f => f.Length);
            }

			Debug.WriteLine("Discovery done");

            actionBlock.Complete();

            await actionBlock.Completion;

			return new Result(
	            totalNumberOfFiles,
	            CreateDuplicateGroups(sizeDico)
	            );
        }

	    private static IEnumerable<DuplicateGroup> CreateDuplicateGroups(
		    ConcurrentDictionary<long, ConcurrentBag<ConcurrentBag<string>>> sizeDico)
	    {
		    foreach (var sizeGroupList in sizeDico)
		    {
			    foreach (var group in sizeGroupList.Value)
			    {
				    if (group.Count > 1)
				    {
						yield return new DuplicateGroup(sizeGroupList.Key, group);
				    }
			    }
		    }
	    }

		private static bool FileCompare(string file1, string file2)
	    {
		    const int BufferSize = 512*1024;

			if (string.Equals(file1, file2))
			{
				return true;
			}

			try
			{
				using var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read);
				using var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read);

				if (fs1.Length != fs2.Length)
				{
					return false;
				}

				var length = fs1.Length;

				if (length == 0)
				{
					return true;
				}

				Span<byte> buffer1 = stackalloc byte[BufferSize];
				Span<byte> buffer2 = stackalloc byte[BufferSize];

				while (true)
				{

					var read1 = fs1.Read(buffer1);
					var read2 = fs2.Read(buffer2);

					if (read1 != read2)
					{
						return false;
					}

					if (read1 <= 0)
					{
						return true;
					}

					if (!buffer1.SequenceEqual(buffer2))
					{
						return false;
					}
				}
			}
			catch
			{
				return false;
			}
		}

		public readonly struct Result
		{
			public readonly int TotalFileScanned;

			public readonly IEnumerable<DuplicateGroup> DuplicateGroups;

			public Result(int totalFileScanned, IEnumerable<DuplicateGroup> duplicateGroups)
			{
				TotalFileScanned = totalFileScanned;
				DuplicateGroups = duplicateGroups;
			}
		}

		public readonly struct DuplicateGroup
		{
			public readonly long FileSize;

			public readonly IReadOnlyCollection<string> FileNames;

			public DuplicateGroup(long fileSize, IReadOnlyCollection<string> fileNames)
			{
				FileSize = fileSize;
				FileNames = fileNames;
			}
		}
    }
}
