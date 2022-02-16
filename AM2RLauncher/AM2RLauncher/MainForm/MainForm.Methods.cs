using Eto.Forms;
using System;

namespace AM2RLauncher
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Method that updates <see cref="progressBar"/>.
        /// </summary>
        /// <param name="value">The value that <see cref="progressBar"/> should be set to.</param>
        /// <param name="min">The min value that <see cref="progressBar"/> should be set to.</param>
        /// <param name="max">The max value that <see cref="progressBar"/> should be set to.</param>
        private void UpdateProgressBar(int value, int min = 0, int max = 100)
        {
            Application.Instance.Invoke(() =>
            {
                progressBar.MinValue = min;
                progressBar.MaxValue = max;
                progressBar.Value = value;
            });
        }


        /// <summary>
        /// Method that updates <see cref="progressBar"/> with a min value of 0 and max value of 100.
        /// </summary>
        /// <param name="value">The value that <see cref="progressBar"/> should be set to.</param>
        private void UpdateProgressBar(int value)
        {
            UpdateProgressBar(value, 0, 100);
        }

        /// <summary>
        /// Safety check function before accessing <see cref="profileIndex"/>.
        /// </summary>
        /// <returns><see langword="true"/> if it is valid, <see langword="false"/> if not.</returns>
        //TODO: this should get a "membernotnullwhen" atttribute whenever i figure out how to apply it here
        private bool IsProfileIndexValid()
        {
            return profileIndex != null;
        }
    }
}