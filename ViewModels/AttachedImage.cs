using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MauiCamera.ViewModels;

public enum AttachedImageStatus
{
    New,
    Edited,
    Saved,
    Deleted
}

public class AttachedImage : ImageProcessingStatus
{
    public AttachedImage() { }

    /// <summary>
    /// Only use when attachment is selected for annotation
    /// To match on the existing attachment list
    /// </summary>
    public Guid? AnnotationGuid { get; set; } = Guid.NewGuid();

    public int? AttachmentId { get; set; }

    public byte[] AttachmentInByteArray { get; set; }

    public int? AttachmentLocalPrimaryKeyId { get; set; }

    public int? EALLocalPrimaryKeyId { get; set; }

    public Guid? AttachmentMobileLocalId { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public Guid? EntityMobileLocalId { get; set; }

    public string EntityType { get; set; }

    public string FileName { get; set; }

    public int? FileSize { get; set; }

    public string FileType { get; set; }

    public ImageFormat format { get; set; }

    public string FullPath { get; set; }

    public ImageSource ImageSource { get; set; }

    public bool IsActionButtonEnabled { get; set; }

    public bool IsDeleteButtonEnabled { get; set; }

    public bool IsEditButtonEnabled { get; set; }
    public bool IsLowRes { get; set; }

    public bool IsPDF { get; set; }

    public bool IsReadOnly { get; set; }
    public bool IsReferenceDocument { get; set; }

    public int LocalPrimaryKeyId { get; set; }

    public int ParentEntityId { get; set; }

    public int PhotoHeight { get; set; }

    public int PhotoWidth { get; set; }
    /// <summary>
    /// Identify whether the image come from step information photo
    /// </summary>
    public int? PreviousAttachmentID { get; set; }

    public virtual bool ProcessingDone => ProcessingId is null;
    public string? ProcessingId { get; set; }
    object ProcessingStatus.ProcessingId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public int Rotation { get; set; }

    public AttachedImageStatus Status { get; set; }

    public byte[] ThumbnailInByteArray { get; set; }
}
