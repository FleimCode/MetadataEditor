using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;

using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using System;
using System.IO;
using System.Text;
using System.Windows;
using MetadataExtractor;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Linq;
using MetadataExtractor.Formats.Exif;
using SixLabors.ImageSharp.Processing;

namespace MetadataEditor
{
    public partial class MainWindow : Window
    {
        private string? _currentImagePath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите файл изображения",
                Filter = "Файлы изображений|*.jpg;*.jpeg;*.png;*.gif;*.tiff;*.bmp|Все файлы|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _currentImagePath = openFileDialog.FileName;
                DisplayMetadata(_currentImagePath);
                DisplayImage(_currentImagePath);
            }
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                _currentImagePath = Path.Combine(Path.GetTempPath(), "pasted_image.png");
                try
                {
                    BitmapSource? bitmap = Clipboard.GetImage();
                    if (bitmap != null)
                    {
                        using (var fileStream = new FileStream(_currentImagePath, FileMode.Create))
                        {
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            encoder.Save(fileStream);
                        }
                        DisplayMetadata(_currentImagePath);
                        DisplayImage(_currentImagePath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при вставке изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Буфер обмена не содержит изображения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DisplayMetadata(string filePath)
        {
            try
            {
                var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);
                var sb = new StringBuilder();

                foreach (var directory in directories)
                {
                    if (directory.Tags.Any())
                    {
                        sb.AppendLine($"--- {directory.Name} ---");
                        foreach (var tag in directory.Tags)
                        {
                            sb.AppendLine($"{tag.Name}: {tag.Description}");
                        }
                        sb.AppendLine();
                    }
                }
                MetadataTextBox.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                MetadataTextBox.Text = $"Ошибка при чтении метаданных: {ex.Message}";
            }
        }

        private void DisplayImage(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                ImagePreview.Source = bitmap;
            }
            catch (Exception)
            {
                ImagePreview.Source = null;
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(MetadataTextBox.Text))
            {
                Clipboard.SetText(MetadataTextBox.Text);
                MessageBox.Show("Метаданные скопированы в буфер обмена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Нет метаданных для копирования.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentImagePath))
            {
                MessageBox.Show("Сначала откройте изображение.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Сохранить файл без метаданных",
                Filter = "Файлы изображений JPEG|*.jpg|Файлы изображений PNG|*.png",
                FileName = Path.GetFileNameWithoutExtension(_currentImagePath) + "_nometa.jpg"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var image = SixLabors.ImageSharp.Image.Load(_currentImagePath))
                    {
                        var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(_currentImagePath);
                        var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                        if (exifIfd0Directory != null && exifIfd0Directory.TryGetUInt16(ExifDirectoryBase.TagOrientation, out var orientation))
                        {
                            switch (orientation)
                            {
                                case 2: image.Mutate(x => x.Flip(FlipMode.Horizontal)); break;
                                case 3: image.Mutate(x => x.Rotate(180)); break;
                                case 4: image.Mutate(x => x.Flip(FlipMode.Vertical)); break;
                                case 5: image.Mutate(x => x.Flip(FlipMode.Horizontal).Rotate(270)); break;
                                case 6: image.Mutate(x => x.Rotate(90)); break;
                                case 7: image.Mutate(x => x.Flip(FlipMode.Horizontal).Rotate(90)); break;
                                case 8: image.Mutate(x => x.Rotate(270)); break;
                            }
                        }

                        image.Metadata.ExifProfile = null;
                        image.Metadata.IptcProfile = null;
                        image.Metadata.XmpProfile = null;
                        image.Metadata.IccProfile = null;

                        string extension = Path.GetExtension(saveFileDialog.FileName).ToLower();

                        if (extension == ".png")
                        {
                            var pngEncoder = new PngEncoder
                            {
                                CompressionLevel = PngCompressionLevel.BestCompression,
                                SkipMetadata = true
                            };
                            image.Save(saveFileDialog.FileName, pngEncoder);
                        }
                        else
                        {
                            var jpegEncoder = new JpegEncoder
                            {
                                Quality = 85,
                                SkipMetadata = true
                            };
                            image.Save(saveFileDialog.FileName, jpegEncoder);
                        }
                    }

                    MessageBox.Show($"Файл успешно сохранён без метаданных: {saveFileDialog.FileName}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);

                    VerifyMetadataRemoved(saveFileDialog.FileName);

                    ClearFile();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void VerifyMetadataRemoved(string filePath)
        {
            try
            {
                var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);
                var metadataCount = 0;

                foreach (var directory in directories)
                {
                    if (!directory.Name.Contains("JPEG") && !directory.Name.Contains("PNG") &&
                        !directory.Name.Contains("File") && !directory.Name.Contains("Image"))
                    {
                        metadataCount += directory.Tags.Count();
                    }
                }

                if (metadataCount > 0)
                {
                    MessageBox.Show($"Внимание: в файле всё ещё обнаружено {metadataCount} тегов метаданных. " +
                                    "Некоторые базовые метаданные могут сохраняться для корректного отображения изображения.",
                                    "Информация", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при проверке метаданных: {ex.Message}");
            }
        }

        private void ClearFile()
        {
            MetadataTextBox.Text = string.Empty;
            _currentImagePath = null;
            ImagePreview.Source = null;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearFile();
        }
    }
}