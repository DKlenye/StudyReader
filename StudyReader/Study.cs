using System.Collections.Generic;

namespace StudyReader
{
    public class Study
    {
        public string Name { get; set; }
        public IList<Module> Modules { get; set; }
    }
}
