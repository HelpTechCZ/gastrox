using System.Collections.ObjectModel;
using Gastrox.Models;
using Gastrox.Services;

namespace Gastrox.ViewModels;

public class PohybyViewModel : ViewModelBase
{
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

    // ---- Init ----

    public PohybyViewModel()
    {
        Refresh();
    }

    public void Refresh()
    {
        Prijemky.Clear();
        foreach (var p in DatabaseService.LoadPrijemky())
            Prijemky.Add(p);

        Vydejky.Clear();
        foreach (var v in DatabaseService.LoadVydejky())
            Vydejky.Add(v);
    }
}
