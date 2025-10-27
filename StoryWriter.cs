﻿using EpubWriter.Util;
using System.Xml.Linq;
using System.IO.Compression;

namespace EpubWriter
{
    public record Series(string Title, int Volume);
    public record Story(string Title, string Language, string Author, Series? Series, string[] Tags, IReadOnlyList<string> Chapters, string? CoverPath = null) 
    { 
        public static DateTime CreatedAt { get; } = DateTime.UtcNow;
		public object Identifier { get; } = Guid.NewGuid();
	}
    
    public static class StoryWriter
    {
        public static void CreateEpub(Story story)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string storyDirectory = Path.Combine(baseDirectory, story.Title);
			ResourceExtractor.WriteEpubManifest(storyDirectory);

            XDocument tocNcx = GenerateTocNcx(story);
            string tocNcxPath = Path.Combine(storyDirectory, "EPUB", "toc.ncx");
            tocNcx.Save(tocNcxPath);

			XDocument contentOpf = GenerateContentOpf(story);
			string contentOpfPath = Path.Combine(storyDirectory, "EPUB", "content.opf");
			contentOpf.Save(contentOpfPath);

			XDocument navXhtml = GenerateNavXhtml(story);
			string navXhtmlPath = Path.Combine(storyDirectory, "EPUB", "nav.xhtml");
			navXhtml.Save(navXhtmlPath);

			XDocument titlePageXhtml = GenerateTitlePage(story);
			string titlePagePath = Path.Combine(storyDirectory, "EPUB", "text", "title_page.xhtml");
			titlePageXhtml.Save(titlePagePath);

			if (!string.IsNullOrEmpty(story.CoverPath))
			{
				string coverDestDir = Path.Combine(storyDirectory, "EPUB", "images");
				Directory.CreateDirectory(coverDestDir);
				string coverDest = Path.Combine(coverDestDir, "cover" + Path.GetExtension(story.CoverPath));
				File.Copy(story.CoverPath, coverDest, true);

				XDocument coverXhtml = GenerateCoverPage(story);
				coverXhtml.Save(Path.Combine(storyDirectory, "EPUB", "text", "cover.xhtml"));
			}


			for (int i = 0; i < story.Chapters.Count; i++)
			{
				string chapter = story.Chapters[i];
				XDocument chapterDoc = GenerateChapterXhtml(
					Path.GetFileNameWithoutExtension(chapter),
					File.ReadAllText(chapter),
					i + 1
				);
				string chapterPath = Path.Combine(storyDirectory, "EPUB", "text", $"ch{i + 1:000}.xhtml");
				chapterDoc.Save(chapterPath);
			}

			string epubFilePath = Path.Combine(baseDirectory, $"{story.Title}.epub");

			if (File.Exists(epubFilePath)) File.Delete(epubFilePath);

			ZipFile.CreateFromDirectory(
				storyDirectory,
				epubFilePath,
				CompressionLevel.NoCompression,
				false
			);

			Directory.Delete(storyDirectory, true);
		}

		private static XDocument GenerateChapterXhtml(string chapterTitle, string chapterText, int chapterNumber)
		{
			XNamespace xhtml = "http://www.w3.org/1999/xhtml";
			XNamespace epub = "http://www.idpf.org/2007/ops";

			string[] paragraphs = chapterText.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

			return new XDocument(
				new XDeclaration("1.0", "utf-8", "yes"),
				new XElement(xhtml + "html",
					new XAttribute(XNamespace.Xmlns + "epub", epub),
					new XElement(xhtml + "head",
						new XElement(xhtml + "meta", new XAttribute("charset", "utf-8")),
						new XElement(xhtml + "title", chapterTitle),
						new XElement(xhtml + "link",
							new XAttribute("rel", "stylesheet"),
							new XAttribute("type", "text/css"),
							new XAttribute("href", "../styles/stylesheet1.css")
						)
					),
					new XElement(xhtml + "body",
						new XElement(xhtml + "h1",
							new XAttribute("id", $"chapter-{chapterNumber:00}"),
							chapterTitle
						),
						paragraphs.Select(p => new XElement(xhtml + "p", p))
					)
				)
			);
		}

		private static XDocument GenerateTitlePage(Story story)
		{
			XNamespace xhtml = "http://www.w3.org/1999/xhtml";
			XNamespace epub = "http://www.idpf.org/2007/ops";

			List<XElement> bodyElements = [new (xhtml + "h1", new XAttribute("class", "title"), story.Title)];

			if (story.Series != null)
			{
				bodyElements.Add(
					new XElement(xhtml + "h2", new XAttribute("class", "series"),
						$"{story.Series.Title} - Volume {story.Series.Volume}")
				);
			}

			return new XDocument(
				new XDeclaration("1.0", "utf-8", "yes"),
				new XElement(xhtml + "html",
					new XAttribute(XNamespace.Xmlns + "epub", epub),
					new XElement(xhtml + "head",
						new XElement(xhtml + "meta", new XAttribute("charset", "utf-8")),
						new XElement(xhtml + "title", story.Title),
						new XElement(xhtml + "link",
							new XAttribute("rel", "stylesheet"),
							new XAttribute("type", "text/css"),
							new XAttribute("href", "../styles/stylesheet1.css")
						)
					),
					new XElement(xhtml + "body", bodyElements)
				)
			);
		}

		private static XDocument GenerateCoverPage(Story story)
		{
			if (string.IsNullOrEmpty(story.CoverPath)) return null!;

			XNamespace xhtml = "http://www.w3.org/1999/xhtml";
			XNamespace epub = "http://www.idpf.org/2007/ops";

			return new XDocument(
				new XDeclaration("1.0", "utf-8", "yes"),
				new XElement(xhtml + "html",
					new XAttribute(XNamespace.Xmlns + "epub", epub),
					new XElement(xhtml + "head",
						new XElement(xhtml + "meta", new XAttribute("charset", "utf-8")),
						new XElement(xhtml + "title", "Cover")
					),
					new XElement(xhtml + "body",
						new XElement(xhtml + "img", new XAttribute("src", $"../images/cover{Path.GetExtension(story.CoverPath)}"),
							new XAttribute("alt", "Cover"))
					)
				)
			);
		}


		private static XDocument GenerateNavXhtml(Story story)
		{
			XNamespace xhtml = "http://www.w3.org/1999/xhtml";
			XNamespace epub = "http://www.idpf.org/2007/ops";

			return new XDocument(
				new XDeclaration("1.0", "utf-8", "yes"),
				new XElement(xhtml + "html",
					new XAttribute(XNamespace.Xmlns + "epub", epub),
					new XAttribute("lang", story.Language),
					new XAttribute(XNamespace.Xml + "lang", story.Language),

					new XElement(xhtml + "head",
						new XElement(xhtml + "meta", new XAttribute("charset", "utf-8")),
						new XElement(xhtml + "title", story.Title),
						new XElement(xhtml + "link",
							new XAttribute("rel", "stylesheet"),
							new XAttribute("type", "text/css"),
							new XAttribute("href", "styles/stylesheet1.css"))
					),

					new XElement(xhtml + "body",
						new XAttribute(epub + "type", "frontmatter"),

						new XElement(xhtml + "nav",
							new XAttribute(epub + "type", "toc"),
							new XAttribute("role", "doc-toc"),
							new XAttribute("id", "toc"),
							new XElement(xhtml + "h1",
								new XAttribute("id", "toc-title"),
								story.Title
							),
							new XElement(xhtml + "ol",
								new XAttribute("class", "toc"),
								GenerateNavLinks(story)
							)
						),

						new XElement(xhtml + "nav",
							new XAttribute(epub + "type", "landmarks"),
							new XAttribute("id", "landmarks"),
							new XAttribute("hidden", "hidden"),
							new XElement(xhtml + "ol",
								new XElement(xhtml + "li",
									new XElement(xhtml + "a",
										new XAttribute("href", "text/title_page.xhtml"),
										new XAttribute(epub + "type", "titlepage"),
										"Title Page"
									)
								),
								new XElement(xhtml + "li",
									new XElement(xhtml + "a",
										new XAttribute("href", "#toc"),
										new XAttribute(epub + "type", "toc"),
										"Table of Contents"
									)
								)
							)
						)
					)
				)
			);
		}

		private static IEnumerable<XElement> GenerateNavLinks(Story story)
		{
			XNamespace xhtml = "http://www.w3.org/1999/xhtml";

			for (int i = 0; i < story.Chapters.Count; i++)
			{
				string href = $"text/ch{i + 1:000}.xhtml#chapter-{i + 1:00}";
				string title = Path.GetFileNameWithoutExtension(story.Chapters[i]);

				yield return new XElement(xhtml + "li",
					new XAttribute("id", $"toc-li-{i + 1}"),
					new XElement(xhtml + "a",
						new XAttribute("href", href),
						title
					)
				);
			}
		}


		private static XDocument GenerateContentOpf(Story story)
		{
			string opfNamespace = "http://www.idpf.org/2007/opf";
			XNamespace dc = "http://purl.org/dc/elements/1.1/";

			List<XElement> metadata =
			[
				new(dc + "title", story.Title),
				new(dc + "language", story.Language),
				new(dc + "creator", story.Author),
				new(dc + "identifier", new XAttribute("id", "bookid"), $"urn:uuid:{story.Identifier}"),
				new(dc + "date", Story.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"))
			];

			if (story.Series != null)
			{
				metadata.Add(new XElement(dc + "relation", $"{story.Series.Title} - Volume {story.Series.Volume}"));
			}

			if (!string.IsNullOrEmpty(story.CoverPath))
			{
				metadata.Add(new XElement("meta", new XAttribute("name", "cover"), new XAttribute("content", "cover-image")));
			}

			return new XDocument(
				new XElement(XName.Get("package", opfNamespace),
					new XAttribute("version", "3.0"),
					new XAttribute("unique-identifier", "bookid"),
					new XElement(XName.Get("metadata", opfNamespace), metadata),
					new XElement(XName.Get("manifest", opfNamespace), GenerateManifestItems(story, opfNamespace)),
					new XElement(XName.Get("spine", opfNamespace), GenerateSpineItems(story, opfNamespace)),
					new XElement(XName.Get("guide", opfNamespace),
						new XElement(XName.Get("reference", opfNamespace),
							new XAttribute("type", "cover"),
							new XAttribute("title", "Cover"),
							new XAttribute("href", "cover.xhtml")
						),
						new XElement(XName.Get("reference", opfNamespace),
							new XAttribute("type", "toc"),
							new XAttribute("title", "Table of Contents"),
							new XAttribute("href", "toc.ncx")
						)
					)
				)
			);
		}


		private static XDocument GenerateTocNcx(Story story)
        {
            string ncxNamespace = "http://www.daisy.org/z3986/2005/ncx/";

            return new XDocument(
                new XElement(XName.Get("ncx", ncxNamespace),
                    new XAttribute("version", "2005-1"),
                    new XElement(XName.Get("head", ncxNamespace),
                        new XElement(XName.Get("meta", ncxNamespace),
                            new XAttribute("name", "dtb:uid"),
                            new XAttribute("content", $"urn:uuid:{story.Identifier}")),
                        new XElement(XName.Get("meta", ncxNamespace),
                            new XAttribute("name", "dtb:depth"),
                            new XAttribute("content", "1")),
                        new XElement(XName.Get("meta", ncxNamespace),
                            new XAttribute("name", "dtb:totalPageCount"),
                            new XAttribute("content", "0")),
                        new XElement(XName.Get("meta", ncxNamespace),
                            new XAttribute("name", "dtb:maxPageNumber"),
                            new XAttribute("content", "0"))
                    ),
                    new XElement(XName.Get("docTitle", ncxNamespace),
                        new XElement(XName.Get("text", ncxNamespace), story.Title)
                    ),
                    new XElement(XName.Get("navMap", ncxNamespace),
                        GenerateNavPoints(story, ncxNamespace)
                    )
                )
            );
		}

		private static IEnumerable<XElement> GenerateManifestItems(Story story, string opfNamespace)
		{
			// Static items
			yield return new XElement(XName.Get("item", opfNamespace),
				new XAttribute("id", "ncx"),
				new XAttribute("href", "toc.ncx"),
				new XAttribute("media-type", "application/x-dtbncx+xml")
			);

			yield return new XElement(XName.Get("item", opfNamespace),
				new XAttribute("id", "nav"),
				new XAttribute("href", "nav.xhtml"),
				new XAttribute("media-type", "application/xhtml+xml"),
				new XAttribute("properties", "nav")
			);

			yield return new XElement(XName.Get("item", opfNamespace),
				new XAttribute("id", "stylesheet1"),
				new XAttribute("href", "styles/stylesheet1.css"),
				new XAttribute("media-type", "text/css")
			);

			yield return new XElement(XName.Get("item", opfNamespace),
				new XAttribute("id", "title_page_xhtml"),
				new XAttribute("href", "text/title_page.xhtml"),
				new XAttribute("media-type", "application/xhtml+xml")
			);

			if (!string.IsNullOrEmpty(story.CoverPath))
			{
				yield return new XElement(XName.Get("item", opfNamespace),
					new XAttribute("id", "cover-image"),
					new XAttribute("href", "images/cover" + Path.GetExtension(story.CoverPath)),
					new XAttribute("media-type", "image/" + Path.GetExtension(story.CoverPath).TrimStart('.'))
				);
			}

			// Chapters
			for (int i = 0; i < story.Chapters.Count; i++)
			{
				string id = $"ch{i + 1:000}_xhtml";
				string href = $"text/ch{i + 1:000}.xhtml";

				yield return new XElement(XName.Get("item", opfNamespace),
					new XAttribute("id", id),
					new XAttribute("href", href),
					new XAttribute("media-type", "application/xhtml+xml")
				);
			}
		}

		private static IEnumerable<XElement> GenerateSpineItems(Story story, string opfNamespace)
		{
			yield return new XElement(XName.Get("itemref", opfNamespace),
				new XAttribute("idref", "title_page_xhtml"),
				new XAttribute("linear", "yes")
			);

			yield return new XElement(XName.Get("itemref", opfNamespace),
				new XAttribute("idref", "nav")
			);

			for (int i = 0; i < story.Chapters.Count; i++)
			{
				string idref = $"ch{i + 1:000}_xhtml";
				yield return new XElement(XName.Get("itemref", opfNamespace),
					new XAttribute("idref", idref)
				);
			}
		}

		private static IEnumerable<XElement> GenerateNavPoints(Story story, string ncxNamespace)
		{
			yield return new XElement(XName.Get("navPoint", ncxNamespace),
				new XAttribute("id", "navPoint-0"),
				new XElement(XName.Get("navLabel", ncxNamespace),
					new XElement(XName.Get("text", ncxNamespace), story.Title)
				),
				new XElement(XName.Get("content", ncxNamespace),
					new XAttribute("src", "text/title.xhtml")
				)
			);

			for (int i = 0; i < story.Chapters.Count; i++)
			{
				yield return new XElement(XName.Get("navPoint", ncxNamespace),
					new XAttribute("id", $"navPoint-{i + 1}"),
					new XElement(XName.Get("navLabel", ncxNamespace),
						new XElement(XName.Get("text", ncxNamespace), Path.GetFileNameWithoutExtension(story.Chapters[i]))
					),
					new XElement(XName.Get("content", ncxNamespace),
						new XAttribute("src", $"text/chapter{i + 1}.xhtml")
					)
				);
			}
		}

	}
}
