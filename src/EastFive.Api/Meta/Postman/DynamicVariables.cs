using System;
namespace EastFive.Postman
{
	/// <summary>
	/// https://learning.postman.com/docs/writing-scripts/script-references/variables-list/
	/// </summary>
	public class DynamicVariables
	{
		public class Common
		{
			/// <summary>
			/// A uuid-v4 style guid
			/// </summary>
			/// <example>611c2e81-2ccb-42d8-9ddc-2d0bfa65c1b4</example>
			public const string Guid = "{{$guid}}";

			/// <summary>
			/// The current UNIX timestamp in seconds
			/// </summary>
			/// <example>1562757107</example>
			public const string Timestamp = "{{$timestamp}}";

			/// <summary>
			/// The current ISO timestamp at zero UTC
			/// </summary>
			/// <example>2020-06-09T21:10:36.177Z</example>
			public const string TimestampIso = "{{$isoTimestamp}}";

			/// <summary>
			/// A random 36-character UUID
			/// </summary>
			/// <example>6929bb52-3ab2-448a-9796-d6480ecad36b</example>
			public const string UUID = "{{$guid}}";
		}

		public class TextNumbersColors
		{
			/// <summary>
			/// A random alpha-numeric character
			/// </summary>
			/// <example>6, "y", "z"</example>
			public const string AlphaNumeric = "{{$randomAlphaNumeric}}";

			/// <summary>
			/// A random boolean value (true/false)
			/// </summary>
			/// <example>true, false</example>
			public const string Boolean = "{{$randomBoolean}}";

			/// <summary>
			/// A random integer between 0 and 1000
			/// </summary>
			/// <example>802, 494, 200</example>
			public const string Int = "{{$randomInt}}";

			/// <summary>
			/// A random color
			/// </summary>
			/// <example>"red", "fuchsia", "grey"</example>
			public const string Color = "{{$randomColor}}";

			/// <summary>
			/// A random hex value
			/// </summary>
			/// <example>"#47594a", "#431e48", "#106f21"</example>
			public const string HexColor = "{{$randomHexColor}}";
		}

		public class Names
		{
			/// <summary>
			/// A random first name
			/// </summary>
			/// <example>Ethan, Chandler, Megane</example>
			public const string FirstName = "{{$randomFirstName}}";

			/// <summary>
			/// A random last name
			/// </summary>
			/// <example>Schaden, Schneider, Willms</example>
			public const string LastName = "{{$randomLastName}}";
		}

		public class Profession
		{
		}

		public class PhoneAddressLocation
		{
		}

		public class Images
		{
			/// <summary>
			/// A random avatar image
			/// </summary>
			/// <example>https://s3.amazonaws.com/uifaces/faces/twitter/johnsmithagency/128.jpg</example>
			public const string AvatarImage = "{{$randomAvatarImage}}";

			/// <summary>
			/// A URL for a random image
			/// </summary>
			/// <example>http://lorempixel.com/640/480</example>
			public const string ImageUrl = "{{$randomImageUrl}}";

			/// <summary>
			/// A URL for a random abstract image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/abstract</example>
			public const string AbstractImageUrl = "{{$randomAbstractImage}}";

			/// <summary>
			/// A URL for a random animal image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/animals</example>
			public const string AnimalImageUrl = "{{$randomAnimalsImage}}";

			/// <summary>
			/// A URL for a random stock business image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/business</example>
			public const string StockBusinessImageUrl = "{{$randomBusinessImage}}";

			/// <summary>
			/// A URL for a random cat image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/cat</example>
			public const string CatImageUrl = "{{$randomCatsImage}}";

			/// <summary>
			/// A URL for a random city image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/city</example>
			public const string CityImageUrl = "{{$randomCityImage}}";

			/// <summary>
			/// A URL for a random food image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/food</example>
			public const string FoodImageUrl = "{{$randomFoodImage}}";

			/// <summary>
			/// A URL for a random nightlife image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/nightlife</example>
			public const string NightlifeImageUrl = "{{$randomNightlifeImage}}";

			/// <summary>
			/// A URL for a random fashion image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/fashion</example>
			public const string FashionImageUrl = "{{$randomFashionImage}}";

			/// <summary>
			/// A URL for a random people image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/people</example>
			public const string PeopleImageUrl = "{{$randomPeopleImage}}";

			/// <summary>
			/// A URL for a random nature image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/nature</example>
			public const string NatureImageUrl = "{{$randomNatureImage}}";

			/// <summary>
			/// A URL for a random sports image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/sports</example>
			public const string SportsImageUrl = "{{$randomSportsImage}}";

			/// <summary>
			/// A URL for a random transportation image
			/// </summary>
			/// <example>http://lorempixel.com/640/480/transportation</example>
			public const string TransportImageUrl = "{{$randomTransportImage}}";

			/// <summary>
			/// A random image data URI
			/// </summary>
			/// <example>data:image/svg+xml;charset=UTF-8,%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20version%3D%221.1%22%20baseProfile%3D%22full%22%20width%3D%22undefined%22%20height%3D%22undefined%22%3E%20%3Crect%20width%3D%22100%25%22%20height%3D%22100%25%22%20fill%3D%22grey%22%2F%3E%20%20%3Ctext%20x%3D%220%22%20y%3D%2220%22%20font-size%3D%2220%22%20text-anchor%3D%22start%22%20fill%3D%22white%22%3Eundefinedxundefined%3C%2Ftext%3E%20%3C%2Fsvg%3E</example>
			public const string DataUri = "{{$randomImageDataUri}}";
		}

		public class Finance
		{
		}

		/// <summary>
        /// Random BS (Business Speak)
        /// </summary>
		public class Business
		{

			/// <summary>
			/// A random company name
			/// </summary>
			/// <example>Johns - Kassulke, Grady LLC</example>
			public const string CompanyName = "{{$randomCompanyName}}";

			/// <summary>
			/// A random company suffix (e.g. Inc, LLC, Group)
			/// </summary>
			/// <example>Inc, LLC, Group</example>
			public const string CompanySuffix = "{{$randomCompanySuffix}}";

			/// <summary>
			/// A random phrase of business speak
			/// </summary>
			/// <example>killer leverage schemas,bricks-and-clicks deploy markets, world-class unleash platforms</example>
			public const string BS = "{{$randomBs}}";

			/// <summary>
			/// A random business speak adjective
			/// </summary>
			/// <example>viral, 24/7, 24/365</example>
			public const string BsAdjective = "{{$randomBsAdjective}}";

			/// <summary>
			/// 	A random business speak buzzword
			/// </summary>
			/// <example>repurpose, harness, transition</example>
			public const string BsBuzz = "{{$randomBsBuzz}}";

			/// <summary>
			/// A random business speak noun
			/// </summary>
			/// <example>e-services, markets, interfaces</example>
			public const string BsNoun = "{{$randomBsNoun}}";
		}

		public class Catchphrases
		{
			/// <summary>
			/// A random catchphrase
			/// </summary>
			/// <example>Future-proofed heuristic open architecture,Quality-focused executive toolset,Grass-roots real-time definition </example>
			public const string CatchPhrase = "{{$randomCatchPhrase}}";

			/// <summary>
			/// A random catchphrase adjective
			/// </summary>
			/// <example>Self-enabling, Business-focused, Down-sized</example>
			public const string CatchPhraseAdjective = "{{$randomCatchPhraseAdjective}}";

			/// <summary>
			/// A random catchphrase descriptor
			/// </summary>
			/// <example>bandwidth-monitored, needs-based, homogeneous</example>
			public const string CatchPhraseDescriptor = "{{$randomCatchPhraseDescriptor}}";

			/// <summary>
			/// Randomly generates a catchphrase noun
			/// </summary>
			/// <example>secured line, superstructure,installation</example>
			public const string CatchPhraseNoun = "{{$randomCatchPhraseNoun}}";
		}

		public class Databases
		{
		}

		public class Dates
		{
			/// <summary>
			/// A random future datetime
			/// </summary>
			/// <example>Tue Mar 17 2020 13:11:50 GMT+0530 (India Standard Time),</example>
			public const string DateFuture = "{{$randomDateFuture}}";

			/// <summary>
			/// A random past datetime
			/// </summary>
			/// <example>Sat Mar 02 2019 09:09:26 GMT+0530 (India Standard Time),</example>
			public const string DatePast = "{{$randomDatePast}}";

			/// <summary>
			/// A random recent datetime
			/// </summary>
			/// <example>Tue Jul 09 2019 23:12:37 GMT+0530 (India Standard Time)</example>
			public const string DateRecent = "{{$randomDateRecent}}";

			/// <summary>
			/// A random weekday
			/// </summary>
			/// <example>Thursday, Friday, Monday</example>
			public const string Weekday = "{{$randomWeekday}}";

			/// <summary>
			/// A random month
			/// </summary>
			/// <example>February, May, January</example>
			public const string Month = "{{$randomMonth}}";
		}

		/// <summary>
		/// Domains, Emails and Usernames
		/// </summary>
		public class Accounts
		{
		}

		/// <summary>
		/// Files and Directories
		/// </summary>
		public class Files
		{
		}

		/// <summary>
		/// Stores, Products & Prices
		/// </summary>
		public class Stores
		{
			/// <summary>
			/// A random price between 0.00 and 1000.00
			/// </summary>
			/// <example>531.55, 488.76, 511.56</example>
			public const string Price = "{{$randomPrice}}";

			/// <summary>
			/// A random product
			/// </summary>
			/// <example>Towels, Pizza, Pants</example>
			public const string Product = "{{$randomProduct}}";

			/// <summary>
			/// A random product adjective
			/// </summary>
			/// <example>Unbranded, Incredible, Tasty</example>
			public const string ProductAdjective = "{{$randomProductAdjective}}";

			/// <summary>
			/// A random product material
			/// </summary>
			/// <example>Steel, Plastic, Frozen</example>
			public const string ProductMaterial = "{{$randomProductMaterial}}";

			/// <summary>
			/// A random product name
			/// </summary>
			/// <example>Handmade Concrete Tuna, Refined Rubber Hat</example>
			public const string ProductName = "{{$randomProductName}}";

			/// <summary>
			/// A random commerce category
			/// </summary>
			/// <example>Tools, Movies, Electronics</example>
			public const string Department = "{{$randomDepartment}}";
		}

		/// <summary>
		/// Grammar
		/// </summary>
		public class Grammar
		{
		}

		/// <summary>
		/// Lorem Ipsum
		/// </summary>
		public class LoremIpsum
		{
		}
	}
}

