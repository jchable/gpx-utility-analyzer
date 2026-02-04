package stats

import "math"

const earthRadius = 6371000 // meters

// Haversine computes the great-circle distance in meters between two points
// given their latitude and longitude in decimal degrees.
func Haversine(lat1, lon1, lat2, lon2 float64) float64 {
	dLat := toRad(lat2 - lat1)
	dLon := toRad(lon2 - lon1)
	a := math.Sin(dLat/2)*math.Sin(dLat/2) +
		math.Cos(toRad(lat1))*math.Cos(toRad(lat2))*
			math.Sin(dLon/2)*math.Sin(dLon/2)
	c := 2 * math.Atan2(math.Sqrt(a), math.Sqrt(1-a))
	return earthRadius * c
}

// Distance3D computes the 3D distance accounting for elevation change.
func Distance3D(lat1, lon1, ele1, lat2, lon2, ele2 float64) float64 {
	d2d := Haversine(lat1, lon1, lat2, lon2)
	dEle := ele2 - ele1
	return math.Sqrt(d2d*d2d + dEle*dEle)
}

func toRad(deg float64) float64 {
	return deg * math.Pi / 180
}
