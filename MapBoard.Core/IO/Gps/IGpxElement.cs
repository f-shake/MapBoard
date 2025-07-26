using System;
using System.Collections.Generic;

namespace MapBoard.IO.Formats.Gps
{
    public interface IGpxElement : ICloneable
    {
        Dictionary<string, string> Extensions { get; set; }
        /// <summary>
        /// 在GPX规范中，位于与<extension/>同层，但为了减少非必要属性，放置在程序的Extensions字典中的节点名
        /// </summary>
        HashSet<string> HiddenElements { get; }
    }
}