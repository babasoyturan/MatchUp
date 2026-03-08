namespace MatchUp.Models.Abstracts
{
    public abstract class EntityBase : IEntityBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAtUtc { get; set; }
    }
}
