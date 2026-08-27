using System;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Pure latest-wins predicate shared by the WPF preview session and its regression tests.</summary>
    public static class ImageAlgorithmPreviewValidity
    {
        public static bool IsCurrent(
            Guid capturedDocumentInstanceId,
            long capturedSourceRevision,
            Guid capturedInvocationId,
            Guid currentDocumentInstanceId,
            long currentSourceRevision,
            Guid currentInvocationId,
            bool isClosedOrCompleted)
        {
            return !isClosedOrCompleted
                && capturedDocumentInstanceId == currentDocumentInstanceId
                && capturedSourceRevision == currentSourceRevision
                && capturedInvocationId == currentInvocationId;
        }
    }
}
