#pragma once

#include "UsfTypedefs.h"

class UsfTableCell
{
public:
	double Value; // NaN as invalid value

public:
	UsfTableCell()
	{
		Value = NAN;
	}

	UsfTableCell(double value)
	{
		Value = value;
	}

	static UsfTableCell Parse(String str)
	{

	}
};
