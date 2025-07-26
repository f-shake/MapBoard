using FzLib;
using MapBoard.IO.Formats.Gps;
using MapBoard.Models;
using System.ComponentModel;

namespace MapBoard.Models
{
    public class GpxAndFileInfo : INotifyPropertyChanged
    {
        private GpxDocument gpx;

        public GpxAndFileInfo(string file)
        {
            File = new SimpleFile(file);
            Gpx = new GpxDocument()
            {
                Time = File.Time,
            };
        }
        private GpxAndFileInfo()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public SimpleFile File { get; private set; }

        public GpxDocument Gpx
        {
            get => gpx;
            set => this.SetValueAndNotify(ref gpx, value, nameof(Gpx));
        }

        public async Task LoadGpxAsync()
        {
            Gpx = await GpxSerializer.LoadMetadatasFromFileAsync(File.FullName);
        }
    }
}
