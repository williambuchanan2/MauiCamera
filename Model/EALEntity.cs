using System;

namespace MauiCamera.Model;
public class EALEntity //: BaseTransactionalEntity<EALEntity, EntitiesAttachmentsLinkDto>
{
    public EALEntity()
    {
    }

    public int AttachmentId { get; set; }

    public Guid? AttachmentMobileLocalId { get; set; }

    public string Caption { get; set; }

    public string CreatedBy { get; set; }

    public DateTime? CreatedDateTime { get; set; }

    public int EntitiesAttachmentsLinkId { get; set; }

    public int EntityId { get; set; }

    public Guid? EntityMobileLocalId { get; set; }

    public string EntityType { get; set; }

    public bool IsDeleted { get; set; }

    public bool? IsReferenceDocument { get; set; }
    
    /// <summary>
    /// Identify if the user created/ modify this attachment on the current record
    /// </summary>
    public bool DidUserSave { get; set; }

    public string LastSavedBy { get; set; }

    public DateTime? LastSavedDateTime { get; set; }

    public new int LocalPrimaryKeyId { get; set; }
    public int? SortOrder { get; set; }

    //public override void MapFromDto(EntitiesAttachmentsLinkDto dto)
    //{
    //    IMapper _mapper = DataMapperModel.SharedInstance;
    //    _mapper.Map(dto, this);
    //}
}