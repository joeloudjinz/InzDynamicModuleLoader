using System.ComponentModel.DataAnnotations;

namespace Example.Module.Common.Data;

public class TestEntity
{
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
}