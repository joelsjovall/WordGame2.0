using System.Text.Json.Serialization;

namespace WordGame.Api.Contracts.V1;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SelectCategoryAction), "selectCategory")]
[JsonDerivedType(typeof(PlaceBidAction), "placeBid")]
[JsonDerivedType(typeof(SubmitSealedBidAction), "submitSealedBid")]
[JsonDerivedType(typeof(CallBluffAction), "callBluff")]
[JsonDerivedType(typeof(SubmitWordAction), "submitWord")]
[JsonDerivedType(typeof(LeaveMatchAction), "leaveMatch")]
[JsonDerivedType(typeof(ForfeitMatchAction), "forfeitMatch")]
public abstract record GameActionRequest(Guid ActionId, string ExpectedVersion);

public sealed record SelectCategoryAction(Guid ActionId, string ExpectedVersion, string CategoryId)
    : GameActionRequest(ActionId, ExpectedVersion);
public sealed record PlaceBidAction(Guid ActionId, string ExpectedVersion, int WordCount, bool Risk = false)
    : GameActionRequest(ActionId, ExpectedVersion);
public sealed record SubmitSealedBidAction(Guid ActionId, string ExpectedVersion, int WordCount, bool Risk = false)
    : GameActionRequest(ActionId, ExpectedVersion);
public sealed record CallBluffAction(Guid ActionId, string ExpectedVersion)
    : GameActionRequest(ActionId, ExpectedVersion);
public sealed record SubmitWordAction(Guid ActionId, string ExpectedVersion, string Word)
    : GameActionRequest(ActionId, ExpectedVersion);
public sealed record LeaveMatchAction(Guid ActionId, string ExpectedVersion)
    : GameActionRequest(ActionId, ExpectedVersion);
public sealed record ForfeitMatchAction(Guid ActionId, string ExpectedVersion)
    : GameActionRequest(ActionId, ExpectedVersion);
