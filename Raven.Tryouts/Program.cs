using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Raven.Client.Document;
using Raven.Client.Indexes;

namespace Raven.Tryouts
{
	internal class Program
	{
		private static readonly string[] indexNames = new[] { "Foo1", "Foo2", "Foo3" };

		private static void Main()
		{
			if (Directory.Exists("Data"))
				Directory.Delete("Data", true);

			using (var documentStore = new DocumentStore())
			{
				documentStore.Configuration.DataDirectory = "Data";
				documentStore.Initialize();

				documentStore.DatabaseCommands.PutIndex("Foo1", new IndexDefinition<Foo>
				{
					Map = docs => from doc in docs select new { doc.PropertyA }
				});
				documentStore.DatabaseCommands.PutIndex("Foo2", new IndexDefinition<Foo>
				{
					Map = docs => from doc in docs select new { doc.PropertyA }
				});
				documentStore.DatabaseCommands.PutIndex("Foo3", new IndexDefinition<Foo>
				{
					Map = docs => from doc in docs select new { doc.PropertyA }
				});

				Console.WriteLine("started");
				var totals = new List<long>();
				var indexing = new List<long>();

				for (int i = 0; i < 1024; i++)
				{
					if(i % 25 == 0 && i > 0)
					{
						Console.WriteLine("{2,-5}: Total {0:#,#}, Indexing: {1:#,#}",
						                  totals[i-1], indexing[i-1], i);
					}
					var sp = Stopwatch.StartNew();
					using (var s = documentStore.OpenSession())
					{
						s.Store(new Foo { PropertyA = "Hello" });
						s.SaveChanges();

						var index = Stopwatch.StartNew();
						foreach (var indexName in indexNames)
						{
							s.LuceneQuery<object>(indexName).WaitForNonStaleResults(TimeSpan.MaxValue).FirstOrDefault();
						}
						indexing.Add(index.ElapsedMilliseconds);
					}
					totals.Add(sp.ElapsedMilliseconds);
				}

				Console.WriteLine("Total {0:#,#}, Total (indexing only) {1:#,#}, Avg {2:#.##}, Avg (indexing only) {3:#.##}",
					totals.Sum(), indexing.Sum(), totals.Average(), indexing.Average());

				//for (var i = 1; i <= 128; i++)
				//{
				//    InsertNewDocumentsAndWaitForStaleIndexes(documentStore, i);
				//}
				//for (var i = 2; i <= 30; i++)
				//{
				//    InsertNewDocumentsAndWaitForStaleIndexes(documentStore, i*128);
				//}
			}
		}

		private static void InsertNewDocumentsAndWaitForStaleIndexes(DocumentStore documentStore, int numberOfDocs)
		{
			var stopwatch = Stopwatch.StartNew();
			var docsToWrite = numberOfDocs;
			while (docsToWrite > 0)
			{
				using (var session = documentStore.OpenSession())
				{
					for (var i = 0; (numberOfDocs > 0) && (i < 128); i++, docsToWrite--)
					{
						session.Store(new Foo { PropertyA = "abc def geh" });
					}
					session.SaveChanges();
				}
			}
			var insertTime = stopwatch.ElapsedMilliseconds;
			stopwatch.Restart();
			using (var session = documentStore.OpenSession())
			{
				foreach (var indexName in indexNames)
				{
					session.LuceneQuery<object>(indexName).WaitForNonStaleResults(TimeSpan.MaxValue).FirstOrDefault();
				}
			}
			var indexingTime = stopwatch.ElapsedMilliseconds;
			Console.WriteLine("{0}, {1}, {2}", numberOfDocs, insertTime, indexingTime);
		}
	}

	public class Foo
	{
		public string Id { get; set; }
		public string PropertyA { get; set; }
	}
}