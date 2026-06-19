using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HPIZ
{
    public sealed class FileOperationError
    {
        public string FilePath { get; }
        public Exception Exception { get; }

        public FileOperationError(string filePath, Exception exception)
        {
            FilePath = filePath;
            Exception = exception;
        }
    }

    public sealed class ExtractionResult
    {
        public int ExtractedFileCount { get; }
        public ReadOnlyCollection<FileOperationError> Errors { get; }

        internal ExtractionResult(int extractedFileCount, IList<FileOperationError> errors)
        {
            ExtractedFileCount = extractedFileCount;
            Errors = new ReadOnlyCollection<FileOperationError>(errors);
        }
    }

    public sealed class ArchiveCreationResult
    {
        public int AddedFileCount { get; }
        public bool ExceedsRecommendedSize { get; }
        public ReadOnlyCollection<FileOperationError> Errors { get; }

        internal ArchiveCreationResult(int addedFileCount, bool exceedsRecommendedSize, IList<FileOperationError> errors)
        {
            AddedFileCount = addedFileCount;
            ExceedsRecommendedSize = exceedsRecommendedSize;
            Errors = new ReadOnlyCollection<FileOperationError>(errors);
        }
    }
}
