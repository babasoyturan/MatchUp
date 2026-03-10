namespace MatchUp.ViewModels.Teams
{
    public class OpenToGameApprovalMemberVm
    {
        public Guid PlayerId { get; set; }
        public string FullName { get; set; } = default!;
        public string StatusText { get; set; } = default!;
        public DateTime? RespondedAtUtc { get; set; }
    }
}
