using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace UniShare.Models
{
    public class OwnerProfileEditViewModel
    {
        [StringLength(80)] public string Program { get; set; }
        [StringLength(40)] public string YearLevel { get; set; }
        [StringLength(80)] public string Campus { get; set; }
        [StringLength(500)] public string Bio { get; set; }
    }

    public class AdminSettingsViewModel
    {
        // Read-only display fields
        public string SchoolEmail { get; set; }
        public string Username { get; set; }

        // Password change (all optional — only applied if NewPassword is filled)
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [StringLength(100, MinimumLength = 8, ErrorMessage = "Min 8 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "Passwords don't match")]
        public string ConfirmNewPassword { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Required"), StringLength(60)] public string FirstName { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(60)] public string LastName { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(40, MinimumLength = 3, ErrorMessage = "Min 3 chars")] public string Username { get; set; }
        [Required(ErrorMessage = "Required"), EmailAddress(ErrorMessage = "Invalid email"), StringLength(180)] public string SchoolEmail { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(100, MinimumLength = 8, ErrorMessage = "Min 8 chars"), DataType(DataType.Password)] public string Password { get; set; }
        [Required(ErrorMessage = "Required"), Compare("Password", ErrorMessage = "Passwords don't match"), DataType(DataType.Password), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(10)] public string Birthdate { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(30)] public string Phone { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(40)] public string Section { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(40)] public string StudentIdNumber { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(80)] public string Program { get; set; }
        [Required(ErrorMessage = "Required"), StringLength(40)] public string YearLevel { get; set; }
        [Required(ErrorMessage = "Document required")]
        [Display(Name = "Student ID or COR Document")]
        public HttpPostedFileBase StudentDocument { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email or username is required")]
        [Display(Name = "Email or Username")]
        public string SchoolEmail { get; set; }

        [Required(ErrorMessage = "Password is required"), DataType(DataType.Password)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }

    public class VerificationReviewViewModel
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string StudentName { get; set; }
        public string SchoolEmail { get; set; }
        public string StudentIdNumber { get; set; }
        public string Program { get; set; }
        public string YearLevel { get; set; }
        public string Status { get; set; }
        public string ReviewNotes { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string SchoolIdUrl { get; set; }
        public string OneByOneUrl { get; set; }
        public string RegistrationFormUrl { get; set; }
    }

    public class VerificationApplicationViewModel
    {
        [Required, StringLength(40)] public string StudentIdNumber { get; set; }
        [Required, StringLength(80)] public string Program { get; set; }
        [Required, StringLength(40)] public string YearLevel { get; set; }
        [Required] public HttpPostedFileBase SchoolIdFile { get; set; }
        [Required] public HttpPostedFileBase OneByOnePhotoFile { get; set; }
        [Required] public HttpPostedFileBase RegistrationFormFile { get; set; }
    }
}
