# Lees de golfbanen (clubs) vanuit de JSON-file
$golfCoursesFile = "golfcourses.json"
if (-Not (Test-Path $golfCoursesFile)) {
    Write-Host "Fout:golfcourses.json niet gevonden!"
    exit
}
$clubs = Get-Content $golfCoursesFile | ConvertFrom-Json

# Controleer of het outputbestand al bestaat
$existingFile = "complete_golf_data.json"
$allGolfClubs = @()

if (Test-Path $existingFile) {
    Write-Host "Bestaande JSON gevonden, laden..."
    $allGolfClubs = Get-Content $existingFile | ConvertFrom-Json
} else {
    Write-Host "Geen bestaand JSON-bestand gevonden, starten vanaf nul."
}

# Set up de sessie met authenticatie cookies
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$session.Cookies.Add((New-Object System.Net.Cookie("EGOLF4USESSID", "p3oo8eitlrp3m6trp0u3r3amub", "/", "m.eg4u.nl")))
$session.Cookies.Add((New-Object System.Net.Cookie("has_teetime", "j", "/", "m.eg4u.nl")))
$session.Cookies.Add((New-Object System.Net.Cookie("username", "hout1", "/", "m.eg4u.nl")))
$session.Cookies.Add((New-Object System.Net.Cookie("club", "dehaenen", "/", "m.eg4u.nl")))

$headers = @{
    "Accept" = "application/json"
    "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0"
}

foreach ($club in $clubs) {
    $clubId = $club.id
    $clubName = $club.naam
    Write-Host "Bezig met ophalen van courses voor club: $clubName"

    # Zoek bestaande club in JSON en maak een array als nodig
    $existingClub = $allGolfClubs | Where-Object { $_.club_id -eq $clubId }
    if (-Not $existingClub) {
        $existingClub = @{
            "club_id" = $clubId
            "club_name" = $clubName
            "courses" = @()
        }
        $allGolfClubs += $existingClub
    }
    if (-Not ($existingClub.courses -is [System.Collections.ArrayList])) {
        $existingClub.courses = @()  # Forceer array-initialisatie
    }

    # Haal alle courses op
    $coursesUrl = "https://m.eg4u.nl/api/clubs/foreign/$clubId/courses"
    try {
        $coursesResponse = Invoke-WebRequest -UseBasicParsing -Uri $coursesUrl -WebSession $session -Headers $headers
        $courses = $coursesResponse.Content | ConvertFrom-Json
    } catch {
        Write-Host "Error bij ophalen courses voor club: $clubName - $_"
        continue
    }

    foreach ($course in $courses) {
        $courseId = $course.id
        $courseName = $course.name

        # Zoek bestaande course en verwijder indien nodig
        $existingClub.courses = @($existingClub.courses | Where-Object { $_.course_id -ne $courseId })

        $courseData = @{
            "course_id" = $courseId
            "course_name" = $courseName
            "tees" = @()
        }

        # Haal alle tees op
        $teesUrl = "https://m.eg4u.nl/api/foreign/courses/$courseId/tees"
        try {
            $teesResponse = Invoke-WebRequest -UseBasicParsing -Uri $teesUrl -WebSession $session -Headers $headers
            $tees = $teesResponse.Content | ConvertFrom-Json
        } catch {
            Write-Host "Error bij ophalen tees voor course: $courseName - $_"
            continue
        }

        foreach ($tee in $tees) {
            $teeId = $tee.id
            $teeName = $tee.name
            $teeData = @{
                "tee_id" = $teeId
                "tee_name" = $teeName
                "holes" = @()
            }

            # Haal alle holes op
            $holesUrl = "https://m.eg4u.nl/api/foreign/courses/$courseId/tees/$teeId/holes"
            try {
                $holesResponse = Invoke-WebRequest -UseBasicParsing -Uri $holesUrl -WebSession $session -Headers $headers
                $holes = $holesResponse.Content | ConvertFrom-Json
            } catch {
                Write-Host "Error bij ophalen holes voor tee: $teeName - $_"
                continue
            }

            $teeData.holes = $holes
            $courseData.tees += $teeData
        }

        $existingClub.courses += $courseData
    }
}

# Sla de JSON correct op
$finalJson = $allGolfClubs | ConvertTo-Json -Depth 10 | Out-String
$finalJson | Out-File -Encoding utf8 $existingFile

Write-Host "Golfbaan data succesvol opgeslagen in $existingFile!"
