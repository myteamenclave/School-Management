namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record ReplaceLineItemsRequest(
    IReadOnlyList<LineItemInput> Items
);

public record LineItemInput(
    Guid? Id,
    string Name,
    decimal Amount,
    int DisplayOrder
);
