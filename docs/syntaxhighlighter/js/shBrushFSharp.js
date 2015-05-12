// Taken from https://gist.github.com/khabinov/1916577

// WTFPL licensed
// scratching the surface of research microsoft com/fsharp/manual/spec2.aspx#_Toc207785562
// omits reserved-ident-formats, reserved-symbolic-sequence, quote-op-*, symbolic-op, ...
SyntaxHighlighter.brushes.FSharp = function()
{
 var keywords = 'abstract and as assert base begin class default delegate do done ' +
  'downcast downto elif else end exception extern false finally for '+
  'fun function if in inherit inline interface internal lazy let ' +
  'match member module mutable namespace new null of open or '+
  'override private public rec return static struct then to '+
  'true try type upcast use val void when while with yield';
  var ocaml = 'asr land lor lsl lsr lxor mod sig';
  var reserved ='atomic break checked component const constraint constructor '+
  'continue eager event external fixed functor global include '+
  'method mixin object parallel process protected pure '+
  'sealed tailcall trait virtual volatile';
  var symbolic = 'let! use! do! yield! return! \\| -> <- \\. : \\( \\) \\[ \\] \\[< >\\] \\[\\| \\|\\] '+
  '\\{ \\} \' # :\\?> :\\? ; ;; :> := _ \\.\\. ::';
 
 this.regexList = [
  { regex: SyntaxHighlighter.regexLib.singleLineCComments,    css: 'comments' },   // one line comments
    { regex: new RegExp('\\(\\*[\\s\\S]*?\\*\\)', 'gm'),   css: 'comments' },   // multiline comments
  { regex: SyntaxHighlighter.regexLib.doubleQuotedString,     css: 'string' },   // strings
    { regex: new RegExp('^\\s*#.*', 'gm'),      css: 'preprocessor' },  // preprocessor tags like #light
  { regex: new RegExp(this.getKeywords(keywords), 'gm'),  css: 'keyword' },  // f# keyword
  //{ regex: new RegExp(this.getKeywords(ocaml), 'gm'),  css: 'color1' },   // caml keyword
  //{ regex: new RegExp(this.getKeywords(reserved), 'gm'),  css: 'color2' },   // reserved keyword
  //{ regex: new RegExp(this.getKeywords(symbolic), 'gm'),  css: 'color3' }   // symbolic keyword
  ];
}
 
SyntaxHighlighter.brushes.FSharp.prototype = new SyntaxHighlighter.Highlighter();
SyntaxHighlighter.brushes.FSharp.aliases = ['f#', 'f-sharp', 'fsharp'];