namespace ClandbusERPIntegration.DTOs
{
    public class UpdateOrderDto
    {
        public string OrderNbr { get; set; } = string.Empty;

        public string OrderType { get; set; } = "SO";

        public string Description { get; set; } = string.Empty;
    }
}