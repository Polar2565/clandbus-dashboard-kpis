namespace ClandbusERPIntegration.DTOs
{
    public class LoginRequestDto
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(320)]
        public string Username { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(500)]
        public string Password { get; set; } = string.Empty;

    }
}
