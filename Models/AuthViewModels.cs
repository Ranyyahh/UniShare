using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace UniShare.Models
{
    public class RegisterViewModel
    {
        [Required, StringLength(60)] public string FirstName { get; set; }
        [Required, StringLength(60)] public string LastName { get; set; }
        [Required, StringLength(40, MinimumLength = 3)] public string Username { get; set; }
        [Required, EmailAddress, StringLength(180)] public string SchoolEmail { get; set; }
        [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)] public string Password { get; set; }
        [Required, Compare("Password"), DataType(DataType.Password), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; }
        [Required, StringLength(10)] public string Birthdate { get; set; }
        [Required, StringLength(30)] public string Phone { get; set; }
        [Required, StringLength(40)] public string Section { get; set; }
        [Required, StringLength(40)] public string StudentIdNumber { get; set; }
        [Required, StringLength(80)] public string Program { get; set; }
        [Required, StringLength(40)] public string YearLevel { get; set; }
        [Required] public HttpPostedFileBase SchoolIdPhoto { get; set; }
        [Required] public HttpPostedFileBase OneByOnePhoto { get; set; }
        [Required] public HttpPostedFileBase RegistrationForm { get; set; }
    }

    public class LoginViewModel
    {
        [Required, EmailAddress] public string SchoolEmail { get; set; }
        [Required, DataType(DataType.Password)] public string Password { get; set; }
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
