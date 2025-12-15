using System;

namespace MauiCamera.Model;

public class AttachmentEntity //: BaseTransactionalEntity<AttachmentEntity, AttachmentDto>
{
    public new int LocalPrimaryKeyId { get; set; }

    public int AttachmentId { get; set; } = 0;

    public int? PreviousAttachmentId { get; set; }

    public string EntityType { get; set; }

    public string FileName { get; set; }

    public string FileType { get; set; }

    public int? FileSize { get; set; }

    public byte[] Data { get; set; }

    public byte[] Thumbnail { get; set; }

    public byte[] LargeThumbnail { get; set; }

    public string CreatedBy { get; set; }

    public DateTime? CreatedDateTime { get; set; }

    public string LastSavedBy { get; set; }

    public DateTime? LastSavedDateTime { get; set; }

    public int? SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public AttachmentEntity()
    {
    }

    //public override void MapFromDto(AttachmentDto dto)
    //{
    //    IMapper _mapper = DataMapperModel.SharedInstance;
    //    _mapper.Map(dto, this);
    //}
}
