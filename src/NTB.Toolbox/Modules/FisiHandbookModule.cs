using System.Text;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed record HandbookArticle(string Category, string Title, string Summary, string Body, string[] Keywords);

internal sealed class FisiHandbookModule : IToolboxModule
{
    private static readonly IReadOnlyList<HandbookArticle> Articles = CreateArticles();

    public string Id => "fisi-handbook";
    public string Title => "FiSi-Handbuch";
    public string Category => "Wissen";
    public string Description => "Offline-Nachschlagewerk für Fachinformatiker Systemintegration mit Suche und Praxis-Checklisten.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["fisi", "handbuch", "ausbildung", "netzwerk", "active directory", "linux", "prüfung", "it-sicherheit"];

    public Control CreateView()
    {
        var search = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Im FiSi-Handbuch suchen …", Margin = new Padding(0, 0, 0, 8) };
        var categories = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        categories.Items.Add("Alle Themen");
        categories.Items.AddRange(Articles.Select(a => a.Category).Distinct().OrderBy(x => x).Cast<object>().ToArray());
        categories.SelectedIndex = 0;

        var articleList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        var title = new Label { Dock = DockStyle.Top, Height = 42, Font = new Font("Segoe UI", 15, FontStyle.Bold), Padding = new Padding(8, 8, 8, 0) };
        var content = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10), DetectUrls = true, BackColor = SystemColors.Window };
        var copy = new Button { Text = "Artikel kopieren", Dock = DockStyle.Bottom, Height = 36 };

        var left = new Panel { Dock = DockStyle.Left, Width = 300, Padding = new Padding(0, 0, 10, 0) };
        left.Controls.Add(articleList);
        left.Controls.Add(categories);
        left.Controls.Add(search);

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(content);
        right.Controls.Add(copy);
        right.Controls.Add(title);

        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        root.Controls.Add(right);
        root.Controls.Add(left);

        IReadOnlyList<HandbookArticle> visible = Articles;

        void RefreshArticles()
        {
            var term = search.Text.Trim();
            var selectedCategory = categories.SelectedItem?.ToString() ?? "Alle Themen";
            visible = Articles.Where(article =>
                (selectedCategory == "Alle Themen" || article.Category == selectedCategory) &&
                (term.Length == 0 || SearchText(article).Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(a => a.Category).ThenBy(a => a.Title).ToArray();

            articleList.BeginUpdate();
            articleList.Items.Clear();
            foreach (var article in visible) articleList.Items.Add($"[{article.Category}] {article.Title}");
            articleList.EndUpdate();
            if (articleList.Items.Count > 0) articleList.SelectedIndex = 0;
            else
            {
                title.Text = "Keine Treffer";
                content.Text = "Für die aktuelle Suche wurden keine Handbuchartikel gefunden.";
            }
        }

        void ShowSelectedArticle()
        {
            if (articleList.SelectedIndex < 0 || articleList.SelectedIndex >= visible.Count) return;
            var article = visible[articleList.SelectedIndex];
            title.Text = article.Title;
            content.Text = $"{article.Summary}\r\n\r\n{article.Body}";
            AppLog.Write($"FiSi-Handbuch geöffnet: {article.Title}");
        }

        search.TextChanged += (_, _) => RefreshArticles();
        categories.SelectedIndexChanged += (_, _) => RefreshArticles();
        articleList.SelectedIndexChanged += (_, _) => ShowSelectedArticle();
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(content.Text)) Clipboard.SetText($"{title.Text}\r\n\r\n{content.Text}");
        };

        RefreshArticles();
        return root;
    }

    private static string SearchText(HandbookArticle article) =>
        string.Join(' ', article.Category, article.Title, article.Summary, article.Body, string.Join(' ', article.Keywords));

    private static IReadOnlyList<HandbookArticle> CreateArticles() =>
    [
        new("Netzwerk", "OSI- und TCP/IP-Modell", "Schichtenmodelle helfen, Netzwerkfehler systematisch einzugrenzen.",
            "OSI 1 Bitübertragung: Kabel, Funk, Link, Signal.\r\nOSI 2 Sicherung: Ethernet, MAC, VLAN, Switch.\r\nOSI 3 Vermittlung: IP, Routing, ICMP.\r\nOSI 4 Transport: TCP, UDP, Ports.\r\nOSI 5–7: Sitzung, Darstellung, Anwendung; praktisch häufig TLS, HTTP, DNS, SMB.\r\n\r\nCheckliste:\r\n1. Link und Adapter prüfen.\r\n2. IP, Maske, Gateway und DNS prüfen.\r\n3. Gateway und Ziel pingen.\r\n4. Namensauflösung testen.\r\n5. Zielport testen.\r\n6. Anwendungsprotokoll und Logs prüfen.", ["osi", "tcp/ip", "schichten", "troubleshooting"]),

        new("Netzwerk", "IPv4, Subnetting und CIDR", "Grundlagen für Adressplanung und Fehleranalyse.",
            "Eine IPv4-Adresse besteht aus 32 Bit. Das Präfix bestimmt den Netzanteil. /24 entspricht 255.255.255.0 und bietet 254 klassisch nutzbare Hostadressen.\r\n\r\nWichtige Schritte:\r\n- Netzadresse: Hostbits auf 0 setzen.\r\n- Broadcast: Hostbits auf 1 setzen.\r\n- Hostbereich liegt dazwischen.\r\n- Anzahl Adressen: 2^(32-Präfix).\r\n\r\nHäufige Netze:\r\n/24 = 256 Adressen, /25 = 128, /26 = 64, /27 = 32, /28 = 16, /29 = 8, /30 = 4.\r\nPrivate Bereiche: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16.", ["ipv4", "subnetting", "cidr", "netzmaske"]),

        new("Netzwerk", "DNS und DHCP", "Namensauflösung und automatische Netzwerkkonfiguration verstehen.",
            "DNS übersetzt Namen in Adressen. Typische Records: A/AAAA, CNAME, MX, PTR, TXT und SRV. Rekursive Resolver fragen autoritative Server ab und cachen Antworten nach TTL.\r\n\r\nDHCP-Ablauf: DORA – Discover, Offer, Request, Acknowledge. Ein Lease enthält typischerweise IP, Maske, Gateway, DNS und Laufzeit.\r\n\r\nDiagnose:\r\n- ipconfig /all\r\n- ipconfig /release und /renew\r\n- ipconfig /flushdns\r\n- nslookup oder Resolve-DnsName\r\n- DHCP-Scope, Reservierungen und Ausschlüsse prüfen.", ["dns", "dhcp", "dora", "records"]),

        new("Netzwerk", "VLAN, Switching und Routing", "Netzsegmentierung und Kommunikation zwischen Netzen.",
            "Ein VLAN trennt Broadcast-Domänen logisch. Access-Ports führen ein VLAN ungetaggt, Trunks transportieren mehrere VLANs per IEEE 802.1Q. Inter-VLAN-Kommunikation benötigt Routing.\r\n\r\nSwitch-Grundlagen:\r\n- MAC-Tabelle lernt Quelladressen.\r\n- Unbekannte Ziele werden geflutet.\r\n- STP verhindert Layer-2-Schleifen.\r\n\r\nRouting:\r\n- längstes Präfix gewinnt.\r\n- Default Route 0.0.0.0/0.\r\n- Statisch oder dynamisch, z. B. OSPF.\r\n\r\nPrüfen: VLAN-Zuweisung, Trunk-Allowed-Liste, Native VLAN, Gateway und Routingtabelle.", ["vlan", "switch", "routing", "802.1q", "stp"]),

        new("Windows & AD", "Active Directory Grundlagen", "Domänen, Objekte, Gruppenrichtlinien und Authentifizierung.",
            "Active Directory Domain Services verwaltet Benutzer, Computer, Gruppen und Richtlinien. Eine Domäne nutzt DNS zwingend für Dienstsuche. Domain Controller replizieren Verzeichnisdaten.\r\n\r\nZentrale Begriffe:\r\n- OU: administrative Struktur und GPO-Verknüpfung.\r\n- Gruppe: Berechtigungsvergabe nach AGDLP/AGUDLP.\r\n- Kerberos: Ticket-basierte Anmeldung.\r\n- LDAP: Verzeichniszugriff.\r\n- FSMO: fünf besondere Rollen.\r\n\r\nPraxis: Änderungen dokumentieren, Gruppen statt Einzelbenutzer berechtigen, getrennte Admin-Konten verwenden und Replikation/DNS gemeinsam prüfen.", ["active directory", "ad", "domain controller", "kerberos", "ldap"]),

        new("Windows & AD", "Gruppenrichtlinien und Berechtigungen", "GPO-Verarbeitung und NTFS-/Freigaberechte.",
            "GPO-Reihenfolge: Lokal, Site, Domäne, OU. Spätere Einstellungen überschreiben frühere, sofern keine Sonderregeln greifen. Sicherheitsfilterung und WMI-Filter grenzen die Anwendung ein.\r\n\r\nDiagnose: gpupdate /force, gpresult /h bericht.html, Ereignisanzeige unter GroupPolicy.\r\n\r\nDateirechte:\r\n- Freigaberecht und NTFS-Recht wirken gemeinsam; effektiv gilt die restriktivere Kombination.\r\n- Verweigern hat meist Vorrang.\r\n- Vererbung und Besitz beachten.\r\n- Berechtigungen über Gruppen vergeben.\r\n\r\nBewährtes Muster: Freigabe weit, NTFS präzise – oder beides konsistent und dokumentiert.", ["gpo", "ntfs", "freigabe", "berechtigungen", "gpresult"]),

        new("Windows & AD", "Windows-Diagnosebefehle", "Schnelle Befehle für Support und Fehleranalyse.",
            "Netzwerk: ipconfig /all, route print, arp -a, netstat -ano, nslookup, tracert, Test-NetConnection.\r\nSystem: systeminfo, tasklist, driverquery, msinfo32, perfmon, resmon.\r\nReparatur: sfc /scannow, DISM /Online /Cleanup-Image /RestoreHealth, chkdsk.\r\nDomäne: whoami /all, nltest, dcdiag, repadmin, gpresult.\r\nDienste: sc query, Get-Service, Get-WinEvent.\r\n\r\nVor Änderungen immer Ist-Zustand, Zeit, Benutzer, betroffene Systeme und Fehlermeldung dokumentieren.", ["windows", "befehle", "powershell", "support"]),

        new("Linux", "Linux-Grundlagen und Dateisystem", "Wichtige Verzeichnisse, Rechte und Befehle.",
            "/etc enthält Konfiguration, /var variable Daten und Logs, /home Benutzerdaten, /tmp temporäre Dateien, /proc Laufzeitinformationen.\r\n\r\nBefehle: ls, cd, pwd, cp, mv, rm, mkdir, find, grep, less, tail, df, du.\r\nRechte: r=4, w=2, x=1; chmod 640 datei setzt rw-r-----. Besitzer mit chown ändern.\r\n\r\nDienste und Logs:\r\n- systemctl status|start|stop|restart dienst\r\n- journalctl -u dienst\r\n- ss -tulpn\r\n- ip addr und ip route\r\n\r\nÄnderungen bevorzugt mit sudo und nachvollziehbar durchführen.", ["linux", "filesystem", "chmod", "systemctl"]),

        new("Virtualisierung & Cloud", "Virtualisierung Grundlagen", "Hypervisor, virtuelle Ressourcen und typische Betriebsaufgaben.",
            "Typ-1-Hypervisor laufen direkt auf Hardware, Typ 2 auf einem Host-Betriebssystem. Virtuelle Maschinen teilen CPU, RAM, Storage und Netzwerk des Hosts.\r\n\r\nWichtige Themen:\r\n- Overcommit bewusst planen.\r\n- Snapshots sind kein Backup.\r\n- Virtuelle Switches und VLANs dokumentieren.\r\n- Storage-Latenz und IOPS überwachen.\r\n- Gasttools und Zeitsynchronisation pflegen.\r\n\r\nVor Wartung: Abhängigkeiten, Backup, Clusterzustand, Ressourcenreserve und Rückfallplan prüfen.", ["virtualisierung", "hyper-v", "vmware", "snapshot", "hypervisor"]),

        new("IT-Sicherheit", "Schutzziele und Sicherheitsgrundlagen", "Vertraulichkeit, Integrität und Verfügbarkeit praktisch anwenden.",
            "CIA: Confidentiality, Integrity, Availability. Ergänzend sind Authentizität, Verbindlichkeit und Nachvollziehbarkeit wichtig.\r\n\r\nGrundmaßnahmen:\r\n- Least Privilege und rollenbasierte Rechte.\r\n- MFA für privilegierte und externe Zugänge.\r\n- Patch- und Schwachstellenmanagement.\r\n- Segmentierung und sichere Standardkonfiguration.\r\n- Verschlüsselung bei Transport und Speicherung.\r\n- Zentrale Logs, Alarmierung und regelmäßige Wiederherstellungstests.\r\n\r\nRisiko = Eintrittswahrscheinlichkeit × Schadensausmaß. Maßnahmen vermeiden, vermindern, übertragen oder akzeptieren Risiken.", ["it-sicherheit", "cia", "mfa", "least privilege", "risiko"]),

        new("IT-Sicherheit", "Incident Response", "Strukturierter Ablauf bei Sicherheitsvorfällen.",
            "Phasen: Vorbereitung, Erkennung, Eindämmung, Beseitigung, Wiederherstellung, Nachbereitung.\r\n\r\nSofort-Checkliste:\r\n1. Vorfallzeit und Melder erfassen.\r\n2. Auswirkungen und betroffene Systeme bestimmen.\r\n3. Beweise erhalten; nicht unüberlegt neu starten.\r\n4. Kommunikations- und Eskalationsweg nutzen.\r\n5. Konten, Netzwerk oder Systeme gezielt isolieren.\r\n6. Ursache beseitigen und Zugangsdaten rotieren.\r\n7. Sauber wiederherstellen und überwachen.\r\n8. Lessons Learned und Maßnahmen dokumentieren.\r\n\r\nKeine verdeckten Ermittlungen oder Gegenangriffe durchführen.", ["incident", "security", "vorfall", "forensik", "eskalation"]),

        new("Backup & Storage", "3-2-1-Backup und Wiederherstellung", "Backups werden erst durch getestete Restores verlässlich.",
            "3-2-1: drei Kopien, zwei unterschiedliche Medientypen, eine Kopie extern/offline. Moderne Ergänzung: unveränderliche oder air-gapped Kopie.\r\n\r\nBegriffe:\r\n- RPO: maximal tolerierter Datenverlust.\r\n- RTO: maximal tolerierte Wiederherstellungszeit.\r\n- Voll, differentiell und inkrementell unterscheiden sich bei Laufzeit und Restore-Kette.\r\n\r\nCheckliste:\r\n- Umfang und Ausschlüsse dokumentieren.\r\n- Verschlüsselung und Schlüssel sichern.\r\n- Monitoring und Benachrichtigung einrichten.\r\n- Regelmäßig Datei-, System- und Desaster-Restore testen.\r\n- Testergebnis mit Dauer und Abweichungen protokollieren.", ["backup", "restore", "3-2-1", "rpo", "rto"]),

        new("Backup & Storage", "RAID und Storage", "Verfügbarkeit, Kapazität und Leistung unterscheiden.",
            "RAID ersetzt kein Backup.\r\nRAID 0: Striping, keine Redundanz.\r\nRAID 1: Spiegelung.\r\nRAID 5: eine Parität, mindestens drei Datenträger.\r\nRAID 6: zwei Paritäten, mindestens vier Datenträger.\r\nRAID 10: Spiegelung plus Striping, gute Leistung und Redundanz.\r\n\r\nStorage-Kennzahlen: IOPS, Durchsatz, Latenz, Queue Depth und Cache. Bei Fehlern immer Controller, Datenträgerzustand, Verkabelung, Multipathing und Dateisystem gemeinsam betrachten.", ["raid", "storage", "san", "nas", "iops"]),

        new("Betrieb & Support", "Systematisches Troubleshooting", "Fehler reproduzierbar eingrenzen statt wahllos Änderungen vorzunehmen.",
            "1. Problem und Sollzustand präzisieren.\r\n2. Umfang bestimmen: ein Benutzer, Standort oder alle?\r\n3. Zeitpunkt, Änderungen und Abhängigkeiten prüfen.\r\n4. Hypothese bilden und mit minimalem Test prüfen.\r\n5. Änderung einzeln durchführen und Wirkung messen.\r\n6. Rückfallmöglichkeit bereithalten.\r\n7. Lösung, Ursache und Prävention dokumentieren.\r\n\r\nHilfreiche Fragen: Was funktioniert noch? Seit wann? Was wurde geändert? Ist der Fehler reproduzierbar? Gibt es Logs, Codes oder Vergleichssysteme?",
            ["troubleshooting", "support", "fehleranalyse", "itil"]),

        new("Betrieb & Support", "ITIL und Servicebetrieb", "Grundbegriffe für Tickets, Änderungen und Servicequalität.",
            "Incident: ungeplante Störung; Ziel ist schnelle Wiederherstellung. Problem: zugrunde liegende Ursache. Change: kontrollierte Änderung. Service Request: standardisierte Anfrage.\r\n\r\nEin gutes Ticket enthält:\r\n- aussagekräftigen Titel\r\n- betroffenen Service und Benutzer\r\n- Zeit und Standort\r\n- genaue Fehlermeldung\r\n- Reproduktionsschritte\r\n- bereits durchgeführte Maßnahmen\r\n- Auswirkung, Dringlichkeit und Priorität\r\n\r\nBei Übergaben: aktueller Stand, nächste Aktion, Verantwortlicher und Termin.", ["itil", "incident", "problem", "change", "ticket"]),

        new("Prüfung & Projekt", "FiSi-Projektdokumentation", "Ein technisches Projekt nachvollziehbar, wirtschaftlich und prüfungsgerecht dokumentieren.",
            "Typische Struktur:\r\n1. Ausgangslage und Projektziel\r\n2. Ist-Analyse und Anforderungen\r\n3. Lösungsalternativen und Entscheidung\r\n4. Zeit-, Kosten- und Ressourcenplanung\r\n5. Umsetzung mit Abweichungen\r\n6. Tests und Abnahme\r\n7. Soll-Ist-Vergleich und Fazit\r\n8. Anhänge, Quellen und Kundendokumentation\r\n\r\nWichtig: Eigenleistung sichtbar machen, Entscheidungen begründen, Datenschutz beachten, sensible Daten anonymisieren und Prüfungsordnung der zuständigen IHK verwenden.", ["ihk", "projekt", "dokumentation", "abschlussprüfung"]),

        new("Prüfung & Projekt", "Wirtschaftlichkeit und Nutzwertanalyse", "Technische Entscheidungen transparent vergleichen.",
            "Kostenarten: Investition, Betrieb, Personal, Wartung, Schulung, Energie und Ausfallrisiko. TCO betrachtet die Gesamtkosten über den Nutzungszeitraum.\r\n\r\nNutzwertanalyse:\r\n1. Kriterien festlegen.\r\n2. Kriterien gewichten; Summe 100 %.\r\n3. Varianten einheitlich bewerten.\r\n4. Bewertung × Gewicht berechnen.\r\n5. Ergebnis plausibilisieren und Risiken separat nennen.\r\n\r\nEine hohe Punktzahl ersetzt keine Muss-Anforderung. Technische, rechtliche und organisatorische Ausschlusskriterien zuerst prüfen.", ["wirtschaftlichkeit", "tco", "nutzwertanalyse", "kosten"])
    ];
}
