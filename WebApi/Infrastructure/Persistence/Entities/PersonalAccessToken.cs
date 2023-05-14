using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Infrastructure.Persistence.Entities;

[Index(nameof(Token), IsUnique = true)]
public class PersonalAccessToken : TimestampedEntity
{
    [Key] public int Id { get; set; }

    [Required] public Guid UserId { get; set; }
    [JsonIgnore] public User User { get; set; }

    [Required] public string Token { get; set; }

    public DateTime? LastUsedAt { get; set; }
}