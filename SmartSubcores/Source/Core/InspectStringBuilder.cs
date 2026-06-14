using System.Text;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Fluent builder for constructing inspect strings with consistent formatting.
	/// Reduces boilerplate of repeated StringBuilder + AppendLine + indent patterns.
	/// </summary>
	public class InspectStringBuilder
	{
		private readonly StringBuilder _sb = new StringBuilder();
		private const string Indent = "  ";

		/// <summary>
		/// Creates a new inspect string builder with an initial header line.
		/// </summary>
		public InspectStringBuilder(string header)
		{
			_sb.Append(header);
		}

		/// <summary>
		/// Appends a new indented feature line.
		/// </summary>
		public InspectStringBuilder AppendFeature(string feature)
		{
			_sb.AppendLine();
			_sb.Append(Indent);
			_sb.Append(feature);
			return this;
		}

		/// <summary>
		/// Appends a new indented feature line only if condition is true.
		/// </summary>
		public InspectStringBuilder AppendFeatureIf(bool condition, string feature)
		{
			if (condition)
				AppendFeature(feature);
			return this;
		}

		/// <summary>
		/// Returns the built inspect string.
		/// </summary>
		public override string ToString() => _sb.ToString();

		/// <summary>
		/// Implicit conversion to string for convenience.
		/// </summary>
		public static implicit operator string(InspectStringBuilder builder) => builder.ToString();
	}
}
