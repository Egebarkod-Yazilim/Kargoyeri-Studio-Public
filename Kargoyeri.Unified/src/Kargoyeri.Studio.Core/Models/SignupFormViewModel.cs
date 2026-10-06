using System.ComponentModel.DataAnnotations;

namespace Kargoyeri.Studio.Core.Models;

public sealed class SignupFormViewModel
{
    [Required(ErrorMessage = "Firma adı zorunlu.")]
    [StringLength(120, MinimumLength = 2)]
    [Display(Name = "Firma adı")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adınız ve soyadınız zorunlu.")]
    [StringLength(80, MinimumLength = 3)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunlu.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
    [StringLength(200)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [StringLength(30)]
    [Display(Name = "Telefon (opsiyonel)")]
    public string? Phone { get; set; }

    [StringLength(11, MinimumLength = 10, ErrorMessage = "VKN 10 hane, TCKN 11 hane olmalıdır.")]
    [Display(Name = "VKN / TCKN (opsiyonel)")]
    public string? Vkn { get; set; }

    [Required(ErrorMessage = "Sözleşmeyi onaylamalısınız.")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "Sözleşmeyi onaylamalısınız.")]
    [Display(Name = "KVKK ve Üyelik Sözleşmesi'ni okudum, onaylıyorum")]
    public bool AcceptTerms { get; set; }

    /// <summary>
    /// Kayıt sırasında seçilen pazaryeri kanal kodları (örn: "trendyol", "hepsiburada").
    /// Onaylandıktan sonra tenant metadata'sına işlenir; credential girişi sonradan yapılır.
    /// </summary>
    [Display(Name = "Kullanmak istediğiniz pazaryerleri")]
    public List<string> SelectedChannels { get; set; } = new();

    /// <summary>
    /// Hangi alternatif kaynakları (NopCommerce, Embedded mini-shop) kullanacak — opsiyonel ön seçim.
    /// </summary>
    [Display(Name = "Diğer sipariş kaynakları")]
    public List<string> SelectedExtraSources { get; set; } = new();
}
