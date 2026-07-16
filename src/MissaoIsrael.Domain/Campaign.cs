namespace MissaoIsrael.Domain;

public sealed class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "PROJETO ENVIO ISRAEL";
    public string Slug { get; set; } = "projeto-envio-israel";
    public string Title { get; set; } = "Existe um chamado. Existe uma missão. E nós podemos fazer parte desse envio.";
    public string Description { get; set; } = "Nem todos irão. Mas todos podem fazer parte do envio.";
    public string BibleReference { get; set; } = "Romanos 10:15";
    public string BibleText { get; set; } = "E como pregarão, se não forem enviados?";
    public string HeroImageUrl { get; set; } = "/assets/jerusalem-hero.png";
    public string PurposeImageUrl { get; set; } = "/assets/jerusalem-hero.png";
    public string PurposeHeading { get; set; } = "Por que enviar nosso pastor para Israel?";
    public string Purpose { get; set; } = "Israel não é apenas um destino. É palco onde a história da nossa fé aconteceu.\n\nEsta viagem tem como objetivo proporcionar ao nosso pastor uma experiência nas terras bíblicas, fortalecendo sua visão, renovando seu chamado e trazendo ainda mais profundidade sobre a Palavra e o propósito de Deus para nossa igreja.\n\nSua contribuição envia. Sua generosidade transforma.";
    public List<string> Pillars { get; set; } =
    [
        "Enriquecimento espiritual",
        "Mais autoridade e profundidade na Palavra",
        "Impacto direto na igreja",
        "Conexão com as raízes da fé"
    ];
    public string ContributionHeading { get; set; } = "Sua contribuição envia. Sua generosidade transforma.";
    public List<decimal> QuickAmounts { get; set; } = [50m, 100m, 250m, 500m];
    public decimal GoalAmount { get; set; } = 20000m;
    public string PixKey { get; set; } = "configure-a-chave-pix";
    public string PixQrCodeUrl { get; set; } = "/assets/pix-placeholder.svg";
    public string PastorVideoTitle { get; set; } = "Palavra do Pastor";
    public string PastorVideoSubtitle { get; set; } = "Assista uma mensagem especial";
    public string VideoUrl { get; set; } = "";
    public CampaignStatus Status { get; set; } = CampaignStatus.Inativa;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
