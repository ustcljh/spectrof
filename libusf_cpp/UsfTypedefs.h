#pragma once

#include <string>

template <class baseString>
class extendedString : private baseString
{
public:
	extendedString(baseString str)
	{
		this->~basic_string()
	}

	extendedString Trim(extendedString FilterChars)
	{
		return this->TrimStart(FilterChars).TrimEnd(FilterChars);
	}

	extendedString TrimStart(extendedString FilterChars)
	{
		extendedString::const_iterator it = this->begin();

		while (FilterChars.find(*it, 0) == extendedString::npos)
		{
			++it;
		}

		return this->substr(it - this->cbegin());
	}

	extendedString TrimEnd(extendedString FilterChars)
	{
		extendedString::const_reverse_iterator it = this->crbegin();

		while (FilterChars.find(*it, 0) == extendedString::npos)
		{
			++it;
		}

		return this->substr(0, it - this->crbegin());
	}
};

using String = extendedString<std::string>;
