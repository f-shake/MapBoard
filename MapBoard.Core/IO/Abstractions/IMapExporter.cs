using MapBoard.Mapping.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MapBoard.IO.Abstractions
{
    public interface IMapExporter
    {
        public Task ExportAsync(string path, IEnumerable<IMapLayerInfo> layers);
    }
}
