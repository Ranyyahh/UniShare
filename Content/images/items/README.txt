ITEM PHOTOS - Borrowed Items page
=================================
Put the item pictures in THIS folder (Content/images/items/).

Expected file names (set in Models/Models.cs -> SampleData.Borrowed -> ImageUrl):

  casio-fx991ex.jpg                 Casio fx-991EX ClassWiz Calculator
  engineering-mechanics-statics.jpg Engineering Mechanics (Statics)
  rotring-isograph-pen-set.jpg      Rotring Isograph Junior Pen Set
  molten-gg7x-basketball.jpg        Molten GG7X Official Basketball
  casio-calculator-case.jpg         Casio Calculator Hard Slide-on Case
  arduino-uno-r3-kit.jpg            Arduino Uno R3 Starter Kit

Tips
- Use .jpg, .png or .webp. To use another file name or type, change the ImageUrl text in Models.cs.
- Best size: about 800 x 600 px (4:3), under 300 KB. Any size works - the picture is cropped to fill
  the card image area (object-fit: cover), so keep the item near the middle.
- If a file is missing, the card shows the small drawn icon instead of a broken image.
- In Visual Studio: Solution Explorer -> Show All Files -> right-click the picture -> Include In Project
  (needed when you publish).
