namespace EcommerceSystem.DTOs
{
    public class VariationGroupDto
    {
        public string Name { get; set; } = "";
        public List<VariationValueDto> Values { get; set; } = new();
    }
}