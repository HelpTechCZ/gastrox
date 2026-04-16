using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Gastrox.Commands;
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
                CommandManager.InvalidateRequerySuggested();
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
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ObservableCollection<VydejkaRadek> VydejkaRadky { get; } = new();
    public bool MaVydejkaRadky => VydejkaRadky.Count > 0;

    // ---- Převodky ----

    public ObservableCollection<Prevodka> Prevodky { get; } = new();

    private Prevodka? _vybranaPrevodka;
    public Prevodka? VybranaPrevodka
    {
        get => _vybranaPrevodka;
        set
        {
            if (SetProperty(ref _vybranaPrevodka, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    // ---- PDF tisk ----

    public ICommand TisknoutPrijemkuCommand { get; }
    public ICommand TisknoutVydejkuCommand { get; }
    public ICommand TisknoutPrevodkuCommand { get; }

    // ---- Init ----

    public PohybyViewModel()
    {
        Sklady.Clear();
        foreach (var s in DatabaseService.LoadSklady())
            Sklady.Add(s);

        TisknoutPrijemkuCommand = new RelayCommand(_ => TisknoutPrijemku(), _ => VybranaPrijemka is not null);
        TisknoutVydejkuCommand  = new RelayCommand(_ => TisknoutVydejku(),  _ => VybranaVydejka  is not null);
        TisknoutPrevodkuCommand = new RelayCommand(_ => TisknoutPrevodku(), _ => VybranaPrevodka is not null);

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

    private void TisknoutPrijemku()
    {
        if (_vybranaPrijemka is null) return;
        try
        {
            var radky = DatabaseService.LoadPrijemkaRadky(_vybranaPrijemka.Id);
            PdfService.GenerujPrijemkuPdf(_vybranaPrijemka, radky);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při generování PDF:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TisknoutVydejku()
    {
        if (_vybranaVydejka is null) return;
        try
        {
            var radky = DatabaseService.LoadVydejkaRadky(_vybranaVydejka.Id);
            PdfService.GenerujVydejkuPdf(_vybranaVydejka, radky);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při generování PDF:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TisknoutPrevodku()
    {
        if (_vybranaPrevodka is null) return;
        try
        {
            var radky = DatabaseService.LoadPrevodkaRadky(_vybranaPrevodka.Id);
            PdfService.GenerujPrevodkuPdf(_vybranaPrevodka, radky);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Chyba při generování PDF:\n" + ex.Message, "Chyba",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
