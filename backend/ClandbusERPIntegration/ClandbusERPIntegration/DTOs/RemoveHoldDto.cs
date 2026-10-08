namespace ClandbusERPIntegration.DTOs
{
    public class RemoveHoldDto
    {
        public string OrderNbr { get; set; } = string.Empty;

        public string OrderType { get; set; } = "SO";
    }
}