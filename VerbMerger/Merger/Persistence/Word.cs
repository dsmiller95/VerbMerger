namespace VerbMerger.Merger.Persistence;

public record struct Word(string Text, PartOfSpeech PartOfSpeech)
{
    public static Word Noun(string text) => new(text, PartOfSpeech.Noun);
    public static Word Verb(string text) => new(text, PartOfSpeech.Verb);
    
}

public static class WordExtensions
{
    public static IEnumerable<Word> ToWords(this MergeInput input)
    {
        yield return Word.Noun(input.Subject);
        yield return Word.Verb(input.Verb);
        yield return Word.Noun(input.Object);
    }
    
    public static IEnumerable<Word> ToWords(this MergeOutput output)
    {
        yield return new(output.Word, output.PartOfSpeech);
    }
    
    public static MergeOutput ToMergeOutput(this Word word) => new(word.Text, word.PartOfSpeech);
}