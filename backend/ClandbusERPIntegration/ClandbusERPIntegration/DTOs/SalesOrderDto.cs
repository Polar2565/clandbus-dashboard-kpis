namespace ClandbusERPIntegration.DTOs
{
    public class SalesOrderDto
    {
        public AcumaticaFieldDto OrderNbr { get; set; }
            = new();

        public AcumaticaFieldDto OrderType { get; set; }
            = new();

        public AcumaticaFieldDto Status { get; set; }
            = new();

        public AcumaticaFieldDto CustomerID { get; set; }
            = new();

        public AcumaticaFieldDto Description { get; set; }
            = new();
    }

    public class AcumaticaFieldDto
    {
        public string value { get; set; }
            = string.Empty;
    }
}