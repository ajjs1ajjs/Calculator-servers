namespace AIResourceCalculator.Models;

public class InfrastructureNode
{
    public string Name { get; set; } = "";
    public string Os { get; set; } = "";
    public double Cpu { get; set; }
    // Частота процесора, ГГц (з матриці діапазонів користувачів для SQL/App/Web; фіксоване
    // значення для Master/Worker/допоміжних вузлів). 0 = не показувати.
    public double Ghz { get; set; }
    public double RamGb { get; set; }
    public int NodeCount { get; set; }
    public string StorageType { get; set; } = "SSD";
    public int StorageGb { get; set; }
    public string StorageType2 { get; set; } = "";
    public int StorageGb2 { get; set; }
    public string StorageType3 { get; set; } = "";
    public int StorageGb3 { get; set; }
    public string StorageType4 { get; set; } = "";
    public int StorageGb4 { get; set; }
    public double MinVersion { get; set; }
    public int Iops { get; set; }
    public string IopsProfile { get; set; } = "";
    // Пропускна здатність диска, MiB/s (послідовні операції). 0 = не показувати.
    public int ThroughputMiBs { get; set; }
    public double Latency { get; set; }
    public int PageFileGb { get; set; }
    public string PageFileType { get; set; } = "";
    public string Notes { get; set; } = "";
    // Версія/редакція СУБД для вузлів БД, напр. "MS SQL Server 2022 Standard". Порожнє для не-БД вузлів.
    public string DbVersion { get; set; } = "";
    // Прапорці "не застосовно" (а не просто не порахували) — керують форматуванням клітинки
    // у звіті (довге тире замість порожньої), щоб відрізняти свідомий N/A від прогалини.
    public bool DiskSplitNotApplicable { get; set; }
    public bool PageFileNotApplicable { get; set; }
    public bool IopsNotApplicable { get; set; }
    // Сума всіх дисків одного вузла (OS + Logs + Data + Content + файл підкачки — теж окремий диск).
    public int DiskPerNodeGb => StorageGb + StorageGb2 + StorageGb3 + StorageGb4 + PageFileGb;
    // Сумарний обсяг дисків з урахуванням кількості вузлів.
    public int TotalStorageGb => DiskPerNodeGb * NodeCount;

    public InfrastructureNode Clone() => (InfrastructureNode)MemberwiseClone();
}
