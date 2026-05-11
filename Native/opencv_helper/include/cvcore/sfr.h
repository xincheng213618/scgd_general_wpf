#pragma once

/**
 * @file sfr.h
 * @brief Unified SFR (Spatial Frequency Response) module interface
 *
 * This file provides the unified interface for SFR calculations including:
 * - Slanted-edge SFR (ISO 12233 standard)
 * - Cylinder target SFR
 *
 * All SFR algorithms are now part of the cvcore namespace.
 */

#include "../algorithm/sfr/sfr_base.h"
#include "../algorithm/sfr/sfr_slanted.h"
#include "../algorithm/sfr/sfr_cylinder.h"
