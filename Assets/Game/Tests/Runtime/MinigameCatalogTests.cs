using System.IO;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Runtime
{
    public sealed class MinigameCatalogTests
    {
        [Test]
        public void Catalog_Selects_Latest_And_Specific_Version()
        {
            var root = Path.Combine(Application.dataPath, "Game", "Minigames");
            var catalog = MinigameCatalog.LoadFromDirectory(root);
            var latest = catalog.GetById("stub_v1");
            Assert.NotNull(latest);
            Assert.AreEqual("0.2.0", latest.version);

            var older = catalog.GetById("stub_v1", "0.1.0");
            Assert.NotNull(older);
            Assert.AreEqual("0.1.0", older.version);
        }
    }
}
