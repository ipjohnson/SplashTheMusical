using AwesomeAssertions;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Parsing;

public class EntityCodesTests
{
	[Test]
	[Arguments("#35;", "#")]
	[Arguments("#quot;", "\"")]
	[Arguments("#9829;", "♥")]
	[Arguments("#128512;", "\U0001F600")]
	[Arguments("#lt;b#gt; #amp; #nbsp;", "<b> &  ")]
	[Arguments("#0;", "�")]
	[Arguments("#1114112;", "�")]
	[Arguments("#99999999999;", "�")]
	[Arguments("#unknown;", "#unknown;")]
	[Arguments("#35", "#35")]
	[Arguments("a # b", "a # b")]
	[Arguments("end#", "end#")]
	[Arguments("##35;", "##")]
	[Arguments("#x2665;", "#x2665;")]
	[Arguments("no codes", "no codes")]
	public void Decodes(string text, string expected) => EntityCodes.Decode(text).Should().Be(expected);

	[Test]
	public void DecodesTextLongerThanTheStackBuffer()
	{
		var text = string.Concat(Enumerable.Repeat("ab #quot;", 100));
		EntityCodes.Decode(text).Should().Be(string.Concat(Enumerable.Repeat("ab \"", 100)));
	}
}
