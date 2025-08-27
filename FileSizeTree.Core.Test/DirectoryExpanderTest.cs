using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace FileSizeTree.Core.Test
{
    [TestClass]
    public class DirectoryExpanderTest
    {
        private TestContext testContextInstance;
        /// <summary>
        ///  Gets or sets the test context which provides
        ///  information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get { return testContextInstance; }
            set { testContextInstance = value; }
        }

        [TestMethod]
        public void TestDirectoryExpander()
        {
            var expander = new DirectoryExpander();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Element element = new Element(expander, ElementType.Directory, "E:\\", null);
            expander.Expand(element);
            sw.Stop();

            TestContext.WriteLine($"DirectoryExpander on C: took {sw.Elapsed}");
        }
    }
}
