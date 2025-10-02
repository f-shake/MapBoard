using MapBoard.Mapping.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace MapBoard.IO.Abstractions
{
    public interface IMemoryLayerImporter
    {
        public ValueTask<IEnumerable<SimpleLayer>> GetLayersAsync(string path);
    }
}
