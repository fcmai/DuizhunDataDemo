using System;
using System.Collections.Generic;
using System.Text;

namespace DuizhunDataDemo
{
  

    public static class MathHelper
    {
        /// <summary>
        /// 计算线段AB相对于A点水平线的夹角（弧度）
        /// </summary>
        /// <param name="ax">A点X坐标</param>
        /// <param name="ay">A点Y坐标</param>
        /// <param name="bx">B点X坐标</param>
        /// <param name="by">B点Y坐标</param>
        /// <returns>夹角 弧度，范围 [-π, π]</returns>
        public static double GetAngleRadian(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            return Math.Atan2(dy, dx);
        }
  
        public static double GetAngleRadianFull(double ax, double ay, double bx, double by)
        {
            double rad = GetAngleRadian(ax, ay, bx, by);
            return rad < 0 ? rad + 2 * Math.PI : rad;
        }
    }
}
