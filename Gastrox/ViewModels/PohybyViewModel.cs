using System.Collections.ObjectModel;
using System.Linq;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

public class PohybyViewModel : ViewModelBase
{
    // ---- Filtr skladu ----
    public ObservableCollection<Sklad> Sklady { get; } = new();

    private Sklad? _vybranySklad;
    public Sklad? VybranySklad
    {
        get => _vybranySklad;
        set { if (SetProperty(ref _vybranySklad, value)) Refresh(); }
    }

    // ---- Příjemky ----

    public ObservableCollection<Prijemka> Prijemky { get; } = new();

    private Prijemka? _vybranaPrijemka;
    public Prijemka? VybranaPrijemka
    {
        get => _vybranaPrijemka;
        set
        {
            if (SetProperty(ref _vybranaPrijemka, value))
            {
                PrijemkaRadky.Clear();
                if (value is not null)
                {
                    foreach (var r in DatabaseService.LoadPrijemkaRadky(value.Id))
                        PrijemkaRadky.Add(r);
                }
                OnPropertyChanged(nameof(MaPrijemkaRadky));
            }
        }
    }

    public ObservableCollection<PrijemkaRadek> PrijemkaRadky { get; } = new();
    public bool MaPrijemkaRadky => PrijemkaRadky.Count > 0;

    // ---- Výdejky ----

    public ObservableCollection<Vydejka> Vydejky { get; } = new();

    private Vydejka? _vybranaVydejka;
    public Vydejka? VybranaVydejka
    {
        get => _vybranaVydejka;
        set
        {
            if (SetProperty(ref _vybranaVydejka, value))
            {
                VydejkaRadky.Clear();
                if (value is not null)
                {
                    foreach (var r in DatabaseService.LoadVydejkaRadky(value.Id))
                        VydejkaRadky.Add(r);
                }
                OnPropertyChanged(nameof(MaVydejkaRadky));
            }
        }
    }

    public ObservableCollection<VydejkaRadek> VydejkaRadky { get; } = new();
    public bool MaVydejkaRadky => VydejkaRadky.Count > 0;

    // ---- Převodky ----

    public ObservableCollection<Prevodka> Prevodky { get; } = new();

    // ---- Init ----

    public PohybyViewModel()
    {
        // Do filtru vložíme jako první položku "všechny sklady" = null id (nereprezentováno v Sklad modelu)
        // Zde necháváme VybranySklad = null jako "všechny".
        Sklady.Clear();
        foreach (var s in DatabaseService.LoadSklady())
            Sklady.Add(s);

        Refresh();
    }

    public void Refresh()
    {
        int? sid = _vybranySklad?.Id;

        Prijemky.Clear();
        foreach (var p in DatabaseService.LoadPrijemky(skladId: sid))
            Prijemky.Add(p);

        Vydejky.Clear();
        foreach (var v in DatabaseService.LoadVydejky(skladId: sid))
            Vydejky.Add(v);

        Prevodky.Clear();
        var vsechny = DatabaseService.LoadPrevodky();
        var filtr = sid is null
            ? vsechny
            : vsechny.Where(p => p.SkladZdrojId == sid || p.SkladCilId == sid).ToList();
        foreach (var p in filtr)
            Prevodky.Add(p);
    }
}
