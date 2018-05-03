using System.Collections.Generic;

namespace StudyReader
{
    public class Study
    {
        public string Name { get; set; }
        public IEnumerable<Module> Modules { get; set; }
    }
}
